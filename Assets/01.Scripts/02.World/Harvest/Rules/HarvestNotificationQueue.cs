using System;
using System.Collections.Generic;

namespace Lighthouse.World.Harvest.Rules
{
    internal sealed class HarvestNotificationQueue<T>
    {
        private readonly Queue<T> _pending = new Queue<T>();
        private readonly Action<T> _dispatch;

        private bool _isPublishing;

        internal HarvestNotificationQueue(Action<T> dispatch)
        {
            _dispatch = dispatch
                ?? throw new ArgumentNullException(nameof(dispatch));
        }

        internal void Publish(T notification)
        {
            _pending.Enqueue(notification);

            if (_isPublishing)
            {
                return;
            }

            _isPublishing = true;

            try
            {
                while (_pending.Count > 0)
                {
                    T next = _pending.Dequeue();
                    _dispatch(next);
                }
            }
            finally
            {
                _isPublishing = false;
            }
        }
    }
}
