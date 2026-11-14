using System;

namespace Tripwire.Tests
{
    /// <summary>テスト用の TripwireBase 実装。protected メンバーを外から叩けるようにする。</summary>
    internal sealed class TestTripwire : TripwireBase
    {
        public override string Id => "test.tripwire";
        public override TripwireKind Kind => TripwireKind.KnownIssue;
        public override string Description => "test description";

        public int ArmCount { get; private set; }
        public int DisarmCount { get; private set; }

        public Exception ThrowOnArm { get; set; }
        public Exception ThrowOnDisarm { get; set; }

        protected override void OnArm()
        {
            ArmCount++;
            if (ThrowOnArm != null) throw ThrowOnArm;
        }

        protected override void OnDisarm()
        {
            DisarmCount++;
            if (ThrowOnDisarm != null) throw ThrowOnDisarm;
        }

        public void InvokeSet(bool tripped, TripContext context = default) => Set(tripped, context);
        public void InvokeFire(TripContext context = default) => Fire(context);
        public void InvokeReportSourceError(Exception exception) => ReportSourceError(exception);
    }
}
