using System;
using System.Collections.Generic;
using NUnit.Framework;
using R3;

namespace Tripwire.R3.Tests
{
    public class ReactiveTripwireTests
    {
        sealed class NegativeValueTripwire : ReactiveTripwire<int>
        {
            readonly Func<Observable<int>> _sourceFactory;
            public int CreateSourceCount;
            public int OnNextCount;
            public Exception ThrowOnNext;

            public NegativeValueTripwire(Func<Observable<int>> sourceFactory) => _sourceFactory = sourceFactory;

            public override string Id => "test.negative";
            public override TripwireKind Kind => TripwireKind.RegressionWatch;
            public override string Description => "value is negative";

            protected override Observable<int> CreateSource()
            {
                CreateSourceCount++;
                return _sourceFactory();
            }

            protected override void OnNext(int value)
            {
                OnNextCount++;
                if (ThrowOnNext != null) throw ThrowOnNext;
                Set(value < 0, $"value={value}");
            }
        }

        List<TripRecord> _records;

        [SetUp]
        public void SetUp() => _records = new List<TripRecord>();

        NegativeValueTripwire Create(Func<Observable<int>> factory)
        {
            var wire = new NegativeValueTripwire(factory);
            wire.OnTripwireActivated += _records.Add;
            return wire;
        }

        [Test]
        public void BeforeArm_DoesNotCreateSource()
        {
            var wire = Create(() => Observable.Never<int>());

            Assert.That(wire.CreateSourceCount, Is.Zero);
        }

        [Test]
        public void PushedValue_IsEvaluated()
        {
            using var subject = new Subject<int>();
            var wire = Create(() => subject);
            wire.Arm();

            subject.OnNext(-1);
            subject.OnNext(2);

            Assert.That(_records, Has.Count.EqualTo(2));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.Tripped));
            Assert.That(_records[1].EventType, Is.EqualTo(TripEventType.Cleared));
            wire.Dispose();
        }

        [Test]
        public void ReactiveProperty_CurrentValueIsEvaluatedOnArm()
        {
            using var property = new ReactiveProperty<int>(-3);
            var wire = Create(() => property);

            wire.Arm();

            Assert.That(wire.IsTripped, Is.True);
            wire.Dispose();
        }

        [Test]
        public void Disarm_DisposesSubscription()
        {
            using var subject = new Subject<int>();
            var wire = Create(() => subject);
            wire.Arm();
            wire.Disarm();

            subject.OnNext(-1);

            Assert.That(wire.OnNextCount, Is.Zero);
        }

        [Test]
        public void Rearm_CreatesSourceAgain()
        {
            using var subject = new Subject<int>();
            var wire = Create(() => subject);

            wire.Arm();
            wire.Disarm();
            wire.Arm();
            subject.OnNext(1);

            Assert.That(wire.CreateSourceCount, Is.EqualTo(2));
            Assert.That(wire.OnNextCount, Is.EqualTo(1), "古い購読は破棄されている");
            wire.Dispose();
        }

        [Test]
        public void OnNextThrows_ReportsSourceErrorAndKeepsSubscription()
        {
            using var subject = new Subject<int>();
            var wire = Create(() => subject);
            wire.Arm();
            var ex = new InvalidOperationException("broken");
            wire.ThrowOnNext = ex;

            Assert.DoesNotThrow(() => subject.OnNext(1));

            Assert.That(_records, Has.Count.EqualTo(1));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.SourceError));
            Assert.That(_records[0].Exception, Is.SameAs(ex));

            wire.ThrowOnNext = null;
            subject.OnNext(-1);
            Assert.That(wire.IsTripped, Is.True);
            wire.Dispose();
        }

        [Test]
        public void OnErrorResume_ReportsSourceError()
        {
            using var subject = new Subject<int>();
            var wire = Create(() => subject);
            wire.Arm();
            var ex = new Exception("resume");

            subject.OnErrorResume(ex);

            Assert.That(_records, Has.Count.EqualTo(1));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.SourceError));
            Assert.That(_records[0].Exception, Is.SameAs(ex));
            wire.Dispose();
        }

        [Test]
        public void CompletedWithFailure_ReportsSourceError()
        {
            using var subject = new Subject<int>();
            var wire = Create(() => subject);
            wire.Arm();
            var ex = new Exception("failed");

            subject.OnCompleted(Result.Failure(ex));

            Assert.That(_records, Has.Count.EqualTo(1));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.SourceError));
            Assert.That(_records[0].Exception, Is.SameAs(ex));
            wire.Dispose();
        }

        [Test]
        public void CompletedWithSuccess_RecordsNothing()
        {
            using var subject = new Subject<int>();
            var wire = Create(() => subject);
            wire.Arm();

            subject.OnCompleted();

            Assert.That(_records, Is.Empty);
            wire.Dispose();
        }

        [Test]
        public void CreateSourceThrows_ReportsSourceError()
        {
            var ex = new InvalidOperationException("no source");
            var wire = Create(() => throw ex);

            Assert.DoesNotThrow(() => wire.Arm());

            Assert.That(_records, Has.Count.EqualTo(1));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.SourceError));
            Assert.That(_records[0].Exception, Is.SameAs(ex));
            Assert.DoesNotThrow(() => wire.Disarm());
        }
    }
}
