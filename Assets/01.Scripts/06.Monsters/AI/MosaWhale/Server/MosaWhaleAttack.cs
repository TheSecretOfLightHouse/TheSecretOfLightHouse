using System;
using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.MosaWhale.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.MosaWhale.Server
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MosaWhaleCycle), typeof(MosaWhaleMovement))]
    public sealed class MosaWhaleAttack : MonoBehaviour
    {
        private readonly HashSet<uint> _hitBoatIds = new HashSet<uint>();
        private readonly List<MonsterBoatTarget> _candidates = new List<MonsterBoatTarget>(4);
        private SeaMonster _monster;
        private MosaWhaleCycle _cycle;
        private MosaWhaleMovement _movement;
        private MosaWhaleTargeting _targeting;
        private SeaMonsterSensor _sensor;
        private ulong _attackId;
        private float _damage;
        private bool _hasAttack;
        private bool _isApplyingDamage;

        public ulong AttackId => _attackId;
        public int HitBoatCount => _hitBoatIds.Count;
        public float Damage => _damage;
        public bool IsDamageWindowActive => isActiveAndEnabled && _monster != null
            && _monster.isServer && _monster.IsInitialized && !_monster.IsDead
            && _hasAttack && _cycle.CanRun && _cycle.IsRunning && !_cycle.IsAttackCancelled
            && _movement.isActiveAndEnabled
            && _cycle.Phase == MosaWhalePhase.Rising && _movement.HasAttackCenter
            && _movement.HasValidDamagePose && _movement.HasCrossedSurface
            && _targeting.HasProtectionData && _damage > 0f
            && NetworkTime.time >= _movement.SurfaceCrossedAt
            && NetworkTime.time - _cycle.PhaseStartedAt < _cycle.PhaseDuration
            && NetworkTime.time - _movement.SurfaceCrossedAt < _cycle.Settings.DamageWindowDuration;

        public event Action<ulong, MonsterBoatTarget> TargetDamaged;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _cycle = GetComponent<MosaWhaleCycle>();
            _movement = GetComponent<MosaWhaleMovement>();
            _targeting = GetComponent<MosaWhaleTargeting>();
            _sensor = GetComponent<SeaMonsterSensor>();
        }

        private void FixedUpdate()
        {
            if (_monster != null && _monster.isServer)
            {
                StepDamage();
            }
        }

        private void OnDisable()
        {
            StopAttack();
        }

        public void HandlePhase(MosaWhalePhase phase)
        {
            if (_monster == null || !_monster.isServer)
            {
                return;
            }
            if (phase == MosaWhalePhase.Diving)
            {
                // 공격 단위는 준비 시작 때만 갱신하고 클립이나 판정 재개 시에는 초기화하지 않는다.
                _attackId++;
                if (_attackId == 0)
                {
                    _attackId++;
                }
                _hitBoatIds.Clear();
                _damage = _monster.Profile.AttackDamage;
                _hasAttack = !float.IsNaN(_damage) && !float.IsInfinity(_damage) && _damage >= 0f;
            }
            else if (phase == MosaWhalePhase.Patrol || phase == MosaWhalePhase.Apex
                || phase == MosaWhalePhase.Reentering || phase == MosaWhalePhase.Recovering
                || phase == MosaWhalePhase.Dead)
            {
                StopAttack();
            }
        }

        public void StopAttack()
        {
            _hasAttack = false;
            _candidates.Clear();
        }

        [Server]
        public void StepDamage()
        {
            if (_isApplyingDamage || !IsDamageWindowActive || !_cycle.ValidateProtection())
            {
                return;
            }
            _isApplyingDamage = true;
            try
            {
                // 피격 알림에서 대상이 파괴·비활성화돼도 등록 목록 변경의 영향을 받지 않도록 복사한다.
                _candidates.Clear();
                for (int i = 0; i < _sensor.Targets.Count; i++)
                {
                    _candidates.Add(_sensor.Targets[i]);
                }
                Vector3 center = _movement.AttackCenter;
                float radiusSquared = _movement.AttackRadius * _movement.AttackRadius;
                for (int i = 0; i < _candidates.Count; i++)
                {
                    if (!IsDamageWindowActive || !_cycle.ValidateProtection())
                    {
                        break;
                    }
                    MonsterBoatTarget target = _candidates[i];
                    if (target == null || target.netId == 0 || _hitBoatIds.Contains(target.netId)
                        || !_targeting.CanDamageTarget(target))
                    {
                        continue;
                    }
                    Vector3 offset = target.transform.position - center;
                    offset.y = 0f;
                    // 표시 원을 확장하지 않고 서버가 확인한 배 중심 위치로만 명중을 판정한다.
                    if (offset.sqrMagnitude > radiusSquared)
                    {
                        continue;
                    }
                    uint boatId = target.netId;
                    _hitBoatIds.Add(boatId);
                    if (target.TryApplyDamage(_damage))
                    {
                        TargetDamaged?.Invoke(_attackId, target);
                    }
                    else
                    {
                        _hitBoatIds.Remove(boatId);
                    }
                }
            }
            finally
            {
                _isApplyingDamage = false;
                _candidates.Clear();
            }
        }
    }
}
