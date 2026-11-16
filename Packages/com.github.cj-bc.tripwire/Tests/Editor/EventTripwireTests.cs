using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Tripwire.Tests
{
    public class EventTripwireTests
    {
        sealed class Source
        {
            public int Value;
            public event Action<int> Changed;
            public event Action Happened;
            public int ChangedSubscriberCount => Changed?.GetInvocationList().Length ?? 0;
            public int HappenedSubscriberCount => Happened?.GetInvocationList().Length ?? 0;
            public void Push(int value) { Value = value; Changed?.Invoke(value); }
            public void Raise() => Happened?.Invoke();
        }

        sealed class NegativeValueTripwire : EventTripwire<int>
        {
            readonly Source _source;
            public bool CheckOnSubscribe;
            public Exception ThrowOnNext;

            public NegativeValueTripwire(Source source) => _source = source;

            public override string Id => "test.negative";
            public override TripwireKind Kind => TripwireKind.RegressionWatch;
            public override string Description => "value is negative";

            protected override void Subscribe(Action<int> handler) => _source.Changed += handler;
            protected override void Unsubscribe(Action<int> handler) => _source.Changed -= handler;

            protected override void OnSubscribed()
            {
                if (CheckOnSubscribe) OnNext(_source.Value);
            }

            protected override void OnNext(int value)
            {
                if (ThrowOnNext != null) throw ThrowOnNext;
                Set(value < 0, $"value={value}");
            }
        }

        sealed class HappenedTripwire : EventTripwire
        {
            readonly Source _source;
            public HappenedTripwire(Source source) => _source = source;

            public override string Id => "test.happened";
            public override TripwireKind Kind => TripwireKind.Unconfirmed;
            public override string Description => "something happened";

            protected override void Subscribe(Action handler) => _source.Happened += handler;
            protected override void Unsubscribe(Action handler) => _source.Happened -= handler;
            protected override void OnNext() => Fire();
        }

        Source _source;
        List<TripRecord> _records;

        [SetUp]
        public void SetUp()
        {
            _source = new Source();
            _records = new List<TripRecord>();
        }

        NegativeValueTripwire CreateLevel()
        {
            var wire = new NegativeValueTripwire(_source);
            wire.OnTripwireActivated += _records.Add;
            return wire;
        }

        // ---- 購読 ----

        [Test]
        public void BeforeArm_DoesNotSubscribe()
        {
            CreateLevel();

            Assert.That(_source.ChangedSubscriberCount, Is.Zero);
        }

        [Test]
        public void Arm_Subscribes_Disarm_Unsubscribes()
        {
            var wire = CreateLevel();

            wire.Arm();
            Assert.That(_source.ChangedSubscriberCount, Is.EqualTo(1));

            wire.Disarm();
            Assert.That(_source.ChangedSubscriberCount, Is.Zero);
        }

        [Test]
        public void Rearm_SubscribesOnlyOnce()
        {
            var wire = CreateLevel();

            wire.Arm();
            wire.Disarm();
            wire.Arm();

            Assert.That(_source.ChangedSubscriberCount, Is.EqualTo(1));
            wire.Dispose();
        }

        // ---- 値の Push ----

        [Test]
        public void PushedValue_IsEvaluated()
        {
            var wire = CreateLevel();
            wire.Arm();

            _source.Push(-1);
            _source.Push(3);

            Assert.That(_records, Has.Count.EqualTo(2));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.Tripped));
            Assert.That(_records[0].Context.Message, Is.EqualTo("value=-1"));
            Assert.That(_records[1].EventType, Is.EqualTo(TripEventType.Cleared));
            wire.Dispose();
        }

        [Test]
        public void PushedValue_AfterDisarm_IsNotEvaluated()
        {
            var wire = CreateLevel();
            wire.Arm();
            wire.Disarm();

            _source.Push(-1);

            Assert.That(_records, Is.Empty);
            Assert.That(wire.IsTripped, Is.False);
        }

        [Test]
        public void OnSubscribed_CanEvaluateCurrentStateOnArm()
        {
            _source.Value = -5;
            var wire = CreateLevel();
            wire.CheckOnSubscribe = true;

            wire.Arm();

            Assert.That(wire.IsTripped, Is.True);
            Assert.That(_records, Has.Count.EqualTo(1));
            wire.Dispose();
        }

        [Test]
        public void OnNextThrows_ReportsSourceErrorAndKeepsSubscription()
        {
            var wire = CreateLevel();
            wire.Arm();
            var ex = new InvalidOperationException("broken");
            wire.ThrowOnNext = ex;

            Assert.DoesNotThrow(() => _source.Push(1));

            Assert.That(_records, Has.Count.EqualTo(1));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.SourceError));
            Assert.That(_records[0].Exception, Is.SameAs(ex));

            wire.ThrowOnNext = null;
            _source.Push(-1);
            Assert.That(wire.IsTripped, Is.True, "例外の後も購読は継続する");
            wire.Dispose();
        }

        // ---- 引数なし版 ----

        [Test]
        public void NonGeneric_RaisedEvent_FiresAndUnsubscribesOnDisarm()
        {
            var wire = new HappenedTripwire(_source);
            wire.OnTripwireActivated += _records.Add;

            wire.Arm();
            _source.Raise();
            _source.Raise();
            wire.Disarm();
            _source.Raise();

            Assert.That(_records, Has.Count.EqualTo(2));
            Assert.That(_records, Has.All.Property(nameof(TripRecord.EventType)).EqualTo(TripEventType.Fired));
            Assert.That(_source.HappenedSubscriberCount, Is.Zero);
        }
    }
}
