using System;
using System.Collections.Generic;

namespace Lighthouse.Mafia.AbyssGauge.Rules
{
    public sealed class AbyssGaugeService
    {
        private readonly Dictionary<uint, CitizenRecord> _citizens = new();
        private readonly HashSet<uint> _participantIds;
        private readonly uint _mafiaId;
        private readonly AbyssBalance _balance;
        private double _lastProcessedAt;
        private double _lastMorningAt;
        private AbyssGaugeSnapshot _finalSnapshot;

        public double BaseAbyss { get; private set; }
        public int LastCompletedDaySerial { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool IsEnded { get; private set; }

        public AbyssGaugeService(IEnumerable<uint> participantIds, uint mafiaId,
            AbyssBalance balance, double startedAt)
        {
            if (participantIds == null)
            {
                throw new ArgumentNullException(nameof(participantIds));
            }
            ValidateFiniteTime(startedAt);
            _participantIds = new HashSet<uint>();
            foreach (uint id in participantIds)
            {
                if (id == 0 || !_participantIds.Add(id))
                {
                    throw new ArgumentException("Actor ids must be nonzero and unique.", nameof(participantIds));
                }
            }
            if (!_participantIds.Contains(mafiaId))
            {
                throw new ArgumentException("The mafia must be a participant.", nameof(mafiaId));
            }
            _mafiaId = mafiaId;
            _balance = balance;
            _lastProcessedAt = startedAt;
            foreach (uint id in _participantIds)
            {
                if (id != mafiaId)
                {
                    _citizens.Add(id, new CitizenRecord { LastRecordedAt = startedAt });
                }
            }
        }

        public bool SettleMorning(int completedDaySerial, double boundaryTime)
        {
            if (IsFrozen || IsEnded)
            {
                return false;
            }
            ValidateFiniteTime(boundaryTime);
            if (completedDaySerial <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(completedDaySerial));
            }
            if (completedDaySerial <= LastCompletedDaySerial)
            {
                if (completedDaySerial == LastCompletedDaySerial && boundaryTime != _lastMorningAt)
                {
                    throw new ArgumentException("A completed day cannot have two boundary times.", nameof(boundaryTime));
                }
                return false;
            }
            if (completedDaySerial != LastCompletedDaySerial + 1)
            {
                throw new ArgumentException("Deliver each completed day in order.", nameof(completedDaySerial));
            }
            ValidateTime(boundaryTime);
            SettleAll(boundaryTime);
            BaseAbyss += _balance.DailyBaseGain;
            LastCompletedDaySerial = completedDaySerial;
            _lastMorningAt = boundaryTime;
            return true;
        }

        public AbyssGaugeSnapshot FreezeForSummon(double serverTime)
        {
            if (IsFrozen)
            {
                return _finalSnapshot;
            }
            if (IsEnded)
            {
                throw new InvalidOperationException("Cannot summon after the match ended.");
            }
            ValidateTime(serverTime);
            SettleAll(serverTime);
            _finalSnapshot = BuildSnapshot(serverTime);
            IsFrozen = true;
            return _finalSnapshot;
        }

        public void EndGame(double serverTime)
        {
            if (IsEnded)
            {
                return;
            }
            if (!IsFrozen)
            {
                ValidateTime(serverTime);
                SettleAll(serverTime);
                _finalSnapshot = BuildSnapshot(serverTime);
            }
            IsEnded = true;
        }

        public AbyssGaugeSnapshot GetSnapshot(double serverTime)
        {
            if (IsFrozen || IsEnded)
            {
                return _finalSnapshot;
            }
            ValidateTime(serverTime);
            return BuildSnapshot(serverTime);
        }

        public bool TryGetCitizen(uint actorId, double serverTime, out CitizenAbyssSnapshot snapshot)
        {
            snapshot = default;
            if (!_citizens.TryGetValue(actorId, out CitizenRecord record))
            {
                return false;
            }
            double time = IsFrozen || IsEnded ? _finalSnapshot.RecordedAt : serverTime;
            ValidateTime(time);
            GetPending(record, time, out double sealSeconds, out double wrongSeconds);
            snapshot = new CitizenAbyssSnapshot(actorId, record.TargetId, record.SettledSeal,
                record.SettledWrong, sealSeconds, wrongSeconds);
            return true;
        }

        
        internal void ChangeVote(uint actorId, uint targetId, double serverTime)
        {
            if (IsFrozen || IsEnded)
            {
                return;
            }
            ValidateTime(serverTime);
            if (!_citizens.TryGetValue(actorId, out CitizenRecord record)
                || !_participantIds.Contains(targetId) || actorId == targetId)
            {
                throw new ArgumentException("The ledger requires an accepted citizen vote.");
            }
            GetPending(record, serverTime, out double sealSeconds, out double wrongSeconds);
            record.PendingWrongSeconds = wrongSeconds;            
            record.SettledSeal = 0;
            record.PendingSealSeconds = 0;
            record.TargetId = targetId;
            record.LastRecordedAt = serverTime;
            _lastProcessedAt = serverTime;
        }

        private void SettleAll(double serverTime)
        {
            foreach (CitizenRecord record in _citizens.Values)
            {
                GetPending(record, serverTime, out double sealSeconds, out double wrongSeconds);
                record.SettledSeal += sealSeconds * _balance.SealGainPerSecond;
                record.SettledWrong += wrongSeconds * _balance.WrongGainPerSecond;
                record.PendingSealSeconds = 0;
                record.PendingWrongSeconds = 0;
                record.LastRecordedAt = serverTime;
            }
            _lastProcessedAt = serverTime;
        }

        private AbyssGaugeSnapshot BuildSnapshot(double serverTime)
        {
            double seal = 0;
            double wrong = 0;
            foreach (CitizenRecord record in _citizens.Values)
            {
                GetPending(record, serverTime, out double sealSeconds, out double wrongSeconds);
                seal += record.SettledSeal + sealSeconds * _balance.SealGainPerSecond;
                wrong += record.SettledWrong + wrongSeconds * _balance.WrongGainPerSecond;
            }
            return new AbyssGaugeSnapshot(serverTime, BaseAbyss, seal, wrong, _balance.MinimumAttackPower);
        }

        private void GetPending(CitizenRecord record, double serverTime,
            out double sealSeconds, out double wrongSeconds)
        {
            sealSeconds = record.PendingSealSeconds;
            wrongSeconds = record.PendingWrongSeconds;
            double elapsed = serverTime - record.LastRecordedAt;
            if (record.TargetId == _mafiaId)
            {
                sealSeconds += elapsed;
            }
            else if (record.TargetId != 0)
            {
                wrongSeconds += elapsed;
            }
        }

        private void ValidateTime(double serverTime)
        {
            ValidateFiniteTime(serverTime);
            if (serverTime < _lastProcessedAt)
            {
                throw new ArgumentOutOfRangeException(nameof(serverTime), "Server events must be chronological.");
            }
        }

        private static void ValidateFiniteTime(double serverTime)
        {
            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime) || serverTime < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serverTime), "Server time must be finite and nonnegative.");
            }
        }

        private sealed class CitizenRecord
        {
            public uint TargetId;
            public double LastRecordedAt;
            public double PendingSealSeconds;
            public double PendingWrongSeconds;
            public double SettledSeal;
            public double SettledWrong;
        }
    }
}
