using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BlessedLine.Desktop;

internal sealed class NetworkBlock
{
    private static readonly Guid LocalInterface = new("4cd62a49-59c3-4969-b7f3-bda5d32890a4");
    private static readonly Guid[] PacketLayers =
    [
        new("c86fd1bf-21cd-497e-a0bb-17425c885c58"),
        new("f52032cb-991c-46e7-971d-2601459a91ca"),
        new("1e5c9fae-8a84-4135-a331-950b54229ecd"),
        new("a3b3ab6b-3564-488c-9117-f34e82142763")
    ];
    private nint _engine;

    internal bool IsActive => _engine != 0;

    public void Apply(string adapterId)
    {
        if (IsActive)
            return;
        if (!Guid.TryParse(adapterId, out var adapter))
            throw new InvalidOperationException("Bu bağlantı türü ağ kesmeyi desteklemiyor.");

        Check(ConvertInterfaceGuidToLuid(ref adapter, out var luid), "Ağ bağlantısı bulunamadı");
        var session = new Session
        {
            Key = Guid.NewGuid(),
            Display = new Display { Name = "BlessedLine kota oturumu" },
            Flags = 1, // FWPM_SESSION_FLAG_DYNAMIC
            Timeout = 1500
        };
        Check(EngineOpen(null, 10, 0, ref session, out var engine), "Ağ kesme oturumu açılamadı");
        try
        {
            Check(TransactionBegin(engine, 0), "Filtre işlemi başlatılamadı");
            var sublayer = new Sublayer
            {
                Key = Guid.NewGuid(),
                Display = new Display { Name = "BlessedLine kota kuralı" },
                Weight = 0x8000
            };
            Check(SublayerAdd(engine, ref sublayer, 0), "Filtre alanı oluşturulamadı");
            AddFilters(engine, sublayer.Key, luid);
            Check(TransactionCommit(engine), "Ağ filtreleri etkinleştirilemedi");
            _engine = engine;
        }
        catch
        {
            TransactionAbort(engine);
            EngineClose(engine);
            throw;
        }
    }

    private static void AddFilters(nint engine, Guid sublayer, ulong luid)
    {
        var value = Marshal.AllocHGlobal(sizeof(ulong));
        var condition = Marshal.AllocHGlobal(Marshal.SizeOf<Condition>());
        try
        {
            Marshal.WriteInt64(value, unchecked((long)luid));
            Marshal.StructureToPtr(new Condition
            {
                Field = LocalInterface,
                Match = 0, // FWP_MATCH_EQUAL
                Value = new Value { Type = 4, Data = value } // FWP_UINT64 points at NET_LUID
            }, condition, false);

            foreach (var layer in PacketLayers)
            {
                var filter = new Filter
                {
                    Key = Guid.NewGuid(),
                    Display = new Display { Name = "BlessedLine kota engeli" },
                    Layer = layer,
                    Sublayer = sublayer,
                    ConditionCount = 1,
                    Conditions = condition,
                    Action = new FilterAction { Type = 0x1001 } // FWP_ACTION_BLOCK
                };
                Check(FilterAdd(engine, ref filter, 0, out _), "Ağ filtresi eklenemedi");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(condition);
            Marshal.FreeHGlobal(value);
        }
    }

    public void Release()
    {
        if (!IsActive)
            return;
        Check(EngineClose(_engine), "Ağ filtresi kaldırılamadı");
        _engine = 0;
    }

    private static void Check(uint status, string message)
    {
        if (status != 0)
            throw new Win32Exception(unchecked((int)status), $"{message} (0x{status:X8}).");
    }

    // Field order follows the x64 layouts of fwpmtypes.h and fwptypes.h.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Display
    {
        public string? Name;
        public string? Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Session
    {
        public Guid Key;
        public Display Display;
        public uint Flags, Timeout, Process;
        public nint Sid, User;
        public int KernelMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob
    {
        public uint Size;
        public nint Bytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Sublayer
    {
        public Guid Key;
        public Display Display;
        public uint Flags;
        public nint Provider;
        public Blob ProviderData;
        public ushort Weight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Value
    {
        public uint Type;
        public nint Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Condition
    {
        public Guid Field;
        public uint Match;
        public Value Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FilterAction
    {
        public uint Type;
        public Guid Callout;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Filter
    {
        public Guid Key;
        public Display Display;
        public uint Flags;
        public nint Provider;
        public Blob ProviderData;
        public Guid Layer, Sublayer;
        public Value Weight;
        public uint ConditionCount;
        public nint Conditions;
        public FilterAction Action;
        public Guid Context;
        public nint Reserved;
        public ulong Id;
        public Value EffectiveWeight;
    }

    [DllImport("iphlpapi.dll")]
    private static extern uint ConvertInterfaceGuidToLuid(ref Guid guid, out ulong luid);
    [DllImport("fwpuclnt.dll", EntryPoint = "FwpmEngineOpen0", CharSet = CharSet.Unicode)]
    private static extern uint EngineOpen(string? server, uint authService, nint authInfo, ref Session session, out nint engine);
    [DllImport("fwpuclnt.dll", EntryPoint = "FwpmEngineClose0")]
    private static extern uint EngineClose(nint engine);
    [DllImport("fwpuclnt.dll", EntryPoint = "FwpmSubLayerAdd0")]
    private static extern uint SublayerAdd(nint engine, ref Sublayer sublayer, nint securityDescriptor);
    [DllImport("fwpuclnt.dll", EntryPoint = "FwpmFilterAdd0")]
    private static extern uint FilterAdd(nint engine, ref Filter filter, nint securityDescriptor, out ulong filterId);
    [DllImport("fwpuclnt.dll", EntryPoint = "FwpmTransactionBegin0")]
    private static extern uint TransactionBegin(nint engine, uint flags);
    [DllImport("fwpuclnt.dll", EntryPoint = "FwpmTransactionCommit0")]
    private static extern uint TransactionCommit(nint engine);
    [DllImport("fwpuclnt.dll", EntryPoint = "FwpmTransactionAbort0")]
    private static extern uint TransactionAbort(nint engine);
}
