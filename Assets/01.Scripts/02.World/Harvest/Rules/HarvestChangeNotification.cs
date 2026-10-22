using System;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    internal static class HarvestChangeNotification
    {
        internal static void Publish<TFirst, TSecond>(
            Action<TFirst, TSecond> handlers,
            TFirst first,
            TSecond second)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<TFirst, TSecond>)subscriber)(first, second);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal static void Publish<TFirst, TSecond, TThird>(
            Action<TFirst, TSecond, TThird> handlers,
            TFirst first,
            TSecond second,
            TThird third,
            Func<bool> canPublish)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate subscriber in handlers.GetInvocationList())
            {
                if (!canPublish())
                {
                    return;
                }

                try
                {
                    ((Action<TFirst, TSecond, TThird>)subscriber)(
                        first, second, third);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
