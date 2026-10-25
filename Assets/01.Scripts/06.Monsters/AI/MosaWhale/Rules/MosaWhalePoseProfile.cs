using UnityEngine;

namespace Lighthouse.Monsters.MosaWhale.Rules
{
    [CreateAssetMenu(fileName = "MosaWhalePoseProfile", menuName = "TheLightHouse/Monster/MosaWhale Pose Profile")]
    public sealed class MosaWhalePoseProfile : ScriptableObject
    {
        [SerializeField] private AnimationCurve[] _diveRotation;
        [SerializeField] private AnimationCurve[] _breachRotation;
        [SerializeField] private AnimationCurve[] _recoveryRotation;
        [SerializeField] private AnimationCurve _breachLift;

        public bool HasValidCurves => HasRotation(_diveRotation) && HasRotation(_breachRotation)
            && HasRotation(_recoveryRotation) && _breachLift != null && _breachLift.length >= 2;

        public Quaternion EvaluateRotation(MosaWhalePhase phase, float progress)
        {
            AnimationCurve[] curves;
            switch (phase)
            {
                case MosaWhalePhase.Diving:
                case MosaWhalePhase.Telegraph:
                    curves = _diveRotation;
                    break;
                case MosaWhalePhase.Rising:
                case MosaWhalePhase.Apex:
                case MosaWhalePhase.Reentering:
                    curves = _breachRotation;
                    break;
                case MosaWhalePhase.Recovering:
                    curves = _recoveryRotation;
                    break;
                default:
                    return Quaternion.identity;
            }
            if (!HasRotation(curves))
            {
                return Quaternion.identity;
            }
            progress = Mathf.Clamp01(progress);
            return new Quaternion(curves[0].Evaluate(progress), curves[1].Evaluate(progress),
                curves[2].Evaluate(progress), curves[3].Evaluate(progress)).normalized;
        }

        public float EvaluateLift(float progress)
        {
            return Mathf.Clamp01(_breachLift.Evaluate(Mathf.Clamp01(progress)));
        }

        private static bool HasRotation(AnimationCurve[] curves)
        {
            return curves != null && curves.Length == 4
                && curves[0] != null && curves[0].length >= 2
                && curves[1] != null && curves[1].length >= 2
                && curves[2] != null && curves[2].length >= 2
                && curves[3] != null && curves[3].length >= 2;
        }
    }
}
