using Lighthouse.World.Ocean.Rules;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class SamplerSlotChecks
    {
        private const int ShipCapacity = 4;
        private const int DriftCapacity = 4;
        private const int FirstShipCount = 3;
        private const int OverflowShipCount = 2;

        public static bool RunAll()
        {
            var slots = new SamplerSlotAllocator(ShipCapacity, DriftCapacity);

            bool isShipFirstOk = slots.TryAllocate(SamplerPool.Ship, FirstShipCount, out SamplerTicket shipFirst) && shipFirst.Start == 0;
            bool isShipFullRejected = !slots.TryAllocate(SamplerPool.Ship, OverflowShipCount, out _);
            bool isDriftStartOk = slots.TryAllocate(SamplerPool.Drift, DriftCapacity, out SamplerTicket drift) && drift.Start == ShipCapacity;
            bool isDriftFullRejected = !slots.TryAllocate(SamplerPool.Drift, 1, out _);

            slots.Release(in shipFirst);
            bool isReuseOk = slots.TryAllocate(SamplerPool.Ship, OverflowShipCount, out SamplerTicket reused) && reused.Start == 0;
            bool isLengthOk = slots.ActiveLength == ShipCapacity + DriftCapacity;

            bool isPassed = isShipFirstOk && isShipFullRejected && isDriftStartOk && isDriftFullRejected && isReuseOk && isLengthOk;
            WaveCheckLog.Report(
                "SlotAllocation",
                isPassed,
                $"ship={isShipFirstOk}, shipFull={isShipFullRejected}, drift={isDriftStartOk}, driftFull={isDriftFullRejected}, reuse={isReuseOk}, length={isLengthOk}");
            return isPassed;
        }
    }
}
