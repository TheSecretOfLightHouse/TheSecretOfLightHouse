using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class WaveDriftChecks
    {
        private const float Mass = 500f;
        private const float Far = 1000f;
        private const float Tolerance = 0.001f;

        public static bool RunAll()
        {
            var profile = ScriptableObject.CreateInstance<WaveDriftProfile>();
            bool isAllPassed = CheckZeroInput(profile);

            isAllPassed &= CheckSlopeDirection(profile);
            isAllPassed &= CheckForceResponse(profile);
            isAllPassed &= CheckSpeedLimit(profile);
            isAllPassed &= CheckShoreFade(profile);
            isAllPassed &= CheckSmoothing(profile);
            isAllPassed &= CheckInvalidInput(profile);

            Object.DestroyImmediate(profile);
            return isAllPassed;
        }

        private static bool CheckZeroInput(WaveDriftProfile profile)
        {
            Vector3 target = WaveDriftModel.ComputeTarget(profile, Vector3.up, Vector3.zero, Mass, Far);
            Vector3 next = WaveDriftModel.Advance(profile, Vector3.zero, target, 0.02f);

            bool isPassed = target == Vector3.zero && next == Vector3.zero;
            WaveCheckLog.Report("Drift.ZeroInput", isPassed, $"target={target}, next={next}");
            return isPassed;
        }

        private static bool CheckSlopeDirection(WaveDriftProfile profile)
        {
            Vector3 downhillX = Target(profile, new Vector3(0.1f, 1f, 0f), Vector3.zero, Far);
            Vector3 uphillX = Target(profile, new Vector3(-0.1f, 1f, 0f), Vector3.zero, Far);
            Vector3 sideZ = Target(profile, new Vector3(0f, 1f, 0.1f), Vector3.zero, Far);

            bool isPassed = Near(downhillX.x, 0.3f) && Near(downhillX.z, 0f) && downhillX.y == 0f
                && Near(uphillX.x, -0.3f) && Near(sideZ.z, 0.3f) && Near(sideZ.x, 0f);
            WaveCheckLog.Report("Drift.SlopeDirection", isPassed, $"down={downhillX}, up={uphillX}, side={sideZ}");
            return isPassed;
        }

        private static bool CheckForceResponse(WaveDriftProfile profile)
        {
            Vector3 force = new Vector3(250f, 100f, 0f);
            Vector3 byForce = Target(profile, Vector3.up, force, Far);
            Vector3 combined = Target(profile, new Vector3(0.1f, 1f, 0f), force, Far);

            float expected = force.x / (Mass * profile.DriftDamping);

            bool isPassed = expected + 0.3f <= profile.MaxDriftSpeed
                && Near(byForce.x, expected) && Near(byForce.z, 0f) && byForce.y == 0f
                && Near(combined.x, expected + 0.3f);
            WaveCheckLog.Report("Drift.ForceResponse", isPassed, $"force={byForce}, combined={combined}");
            return isPassed;
        }

        private static bool CheckSpeedLimit(WaveDriftProfile profile)
        {
            Vector3 strong = Target(profile, Vector3.up, new Vector3(5000f, 0f, 5000f), Far);
            Vector3 slopeAndForce = Target(profile, new Vector3(1f, 1f, 0f).normalized, new Vector3(5000f, 0f, 0f), Far);

            bool isPassed = Near(strong.magnitude, profile.MaxDriftSpeed)
                && Near(strong.x, strong.z)
                && Near(slopeAndForce.magnitude, profile.MaxDriftSpeed);
            WaveCheckLog.Report("Drift.SpeedLimit", isPassed, $"strong={strong.magnitude:F3}, mixed={slopeAndForce.magnitude:F3}");
            return isPassed;
        }

        private static bool CheckShoreFade(WaveDriftProfile profile)
        {
            Vector3 force = new Vector3(250f, 0f, 0f);
            float atStart = Target(profile, Vector3.up, force, profile.ShoreFadeStart).magnitude;
            float atMiddle = Target(profile, Vector3.up, force, (profile.ShoreFadeStart + profile.ShoreFadeEnd) * 0.5f).magnitude;
            float atEnd = Target(profile, Vector3.up, force, profile.ShoreFadeEnd).magnitude;
            float beyond = Target(profile, Vector3.up, force, Far).magnitude;
            float onLand = Target(profile, Vector3.up, force, 0f).magnitude;

            bool isMonotonic = true;
            float previous = 0f;
            for (float d = 0f; d <= 30f; d += 1f)
            {
                float fade = WaveDriftModel.ShoreFade(profile, d);
                isMonotonic &= fade >= previous;
                previous = fade;
            }

            float full = force.x / (Mass * profile.DriftDamping);

            bool isPassed = full <= profile.MaxDriftSpeed
                && Near(atStart, 0f) && Near(atMiddle, full * 0.5f) && Near(atEnd, full)
                && Near(beyond, full) && Near(onLand, 0f) && isMonotonic;
            WaveCheckLog.Report("Drift.ShoreFade", isPassed, $"start={atStart:F3}, mid={atMiddle:F3}, end={atEnd:F3}, mono={isMonotonic}");
            return isPassed;
        }

        private static bool CheckSmoothing(WaveDriftProfile profile)
        {
            Vector3 target = new Vector3(1f, 0f, 0f);
            Vector3 oneStep = WaveDriftModel.Advance(profile, Vector3.zero, target, profile.ResponseTime);
            float expected = 1f - Mathf.Exp(-1f);

            Vector3 stepped = Vector3.zero;
            bool isOvershoot = false;
            int steps = Mathf.RoundToInt(5f * profile.ResponseTime / 0.02f);
            for (int i = 0; i < steps; i++)
            {
                stepped = WaveDriftModel.Advance(profile, stepped, target, 0.02f);
                isOvershoot |= stepped.x > 1f + Tolerance;
            }

            Vector3 coarse = WaveDriftModel.Advance(profile, Vector3.zero, target, 1f);
            Vector3 fine = Vector3.zero;
            for (int i = 0; i < 50; i++)
            {
                fine = WaveDriftModel.Advance(profile, fine, target, 0.02f);
            }

            Vector3 unchangedZero = WaveDriftModel.Advance(profile, target, Vector3.zero, 0f);
            Vector3 unchangedNegative = WaveDriftModel.Advance(profile, target, Vector3.zero, -1f);
            Vector3 huge = WaveDriftModel.Advance(profile, Vector3.zero, target, 1000f);

            bool isPassed = Near(oneStep.x, expected) && stepped.x >= 0.99f && !isOvershoot
                && Near(coarse.x, fine.x) && unchangedZero == target && unchangedNegative == target
                && Near(huge.x, 1f);
            WaveCheckLog.Report("Drift.Smoothing", isPassed, $"one={oneStep.x:F4}, settled={stepped.x:F4}, coarse={coarse.x:F4}, fine={fine.x:F4}");
            return isPassed;
        }

        private static bool CheckInvalidInput(WaveDriftProfile profile)
        {
            Vector3 nanNormal = WaveDriftModel.ComputeTarget(profile, new Vector3(float.NaN, 1f, 0f), Vector3.zero, Mass, Far);
            Vector3 zeroMass = WaveDriftModel.ComputeTarget(profile, Vector3.up, new Vector3(250f, 0f, 0f), 0f, Far);
            Vector3 nullProfile = WaveDriftModel.ComputeTarget(null, Vector3.up, new Vector3(250f, 0f, 0f), Mass, Far);
            Vector3 steepWave = WaveDriftModel.ComputeTarget(profile, new Vector3(1f, 0.01f, 0f), Vector3.zero, Mass, Far);

            bool isPassed = nanNormal == Vector3.zero && zeroMass == Vector3.zero
                && nullProfile == Vector3.zero && steepWave == Vector3.zero;
            WaveCheckLog.Report("Drift.InvalidInput", isPassed, $"nan={nanNormal}, mass={zeroMass}, null={nullProfile}, steep={steepWave}");
            return isPassed;
        }

        private static Vector3 Target(WaveDriftProfile profile, Vector3 normal, Vector3 force, float shoreDistance)
        {
            return WaveDriftModel.ComputeTarget(profile, normal, force, Mass, shoreDistance);
        }

        private static bool Near(float a, float b)
        {
            return Mathf.Abs(a - b) <= Tolerance;
        }
    }
}
