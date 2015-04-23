using System.Drawing;
using System.Windows.Forms;
using System.Reactive.Linq;
using System.Threading;
using System;

namespace ReactiveAnimation {
	public static class LinearAnimation {
		public struct Position<T> {
			public T ObjectToAnimate;
			public Point NewPosition;
			public Point DesiredPosition;
		}
		
		public static int GetPositionOneStepCloserToDestination (int currentPos, int targetPos, int speed) {
			speed = Math.Abs(speed);
			if (currentPos == targetPos) // if it is already at the desired position
				return targetPos; // return it
			var distance = currentPos - targetPos; // determine the distance between the current position and the desired position
			if (Math.Abs(distance) < speed) // if it is less than the speed
				return targetPos; // return the desired position
			return currentPos + ((currentPos > targetPos) ? -speed : speed); // return the value after the speed (towards the desired position) has been applied
		}
		
		private static Position<T> GetNewPosition<T> (T objectToAnimate, Func<T, Point> currentPosition, Point desiredPosition, int speed) {
			var c = currentPosition(objectToAnimate);
			var p = new Position<T> {
				ObjectToAnimate = objectToAnimate, 
				DesiredPosition = desiredPosition,
				NewPosition = new Point(GetPositionOneStepCloserToDestination(c.X, desiredPosition.X, speed), GetPositionOneStepCloserToDestination(c.Y, desiredPosition.Y, speed))
			};
			return p;
		}
		
		public static IObservable<Position<T>> CreateObservable<T> (T objectToAnimate, Func<T, Point> currentPosition, IObservable<Point> toPosition, IObservable<int> speed) {
			return Animation.EveryFrame.CombineLatest(toPosition, speed, (f, p, s) => new { f, p, s }).DistinctUntilChanged(v => v.f).Select(v => GetNewPosition(objectToAnimate, currentPosition, v.p, v.s));
		}
		
		public static CancellationTokenSource AnimateControl (Control ctrl, IObservable<Point> toPosition, IObservable<int> speed, bool keepRelativePosition) {
			var cts = new CancellationTokenSource();
			if (keepRelativePosition)
				KeepRelativePosition(ctrl, toPosition, cts);
			var move = CreateObservable(ctrl, c => ctrl.Location, toPosition, speed);
			move.ObserveOn(ctrl).Subscribe(np => {
				np.ObjectToAnimate.Location = np.NewPosition;
				if (np.DesiredPosition == np.NewPosition)
					cts.Cancel();
			}, cts.Token);
			return cts;
		}
		
		public static CancellationTokenSource KeepRelativePosition (Control ctrl, IObservable<Point> relativeTo, CancellationTokenSource cts = null) {
			Func<int, int, int, int> getRelativePosition = (current, previousFinal, newFinal) => { if (previousFinal == 0) return current; double perc = (double)current / (double)previousFinal; return (int)((double)newFinal * perc); };
			if (cts == null)
				cts = new CancellationTokenSource();
			
			ObservableHelper.ObserveWithPrevious(
				relativeTo.DistinctUntilChanged(),
				(prev, current) => new { prev, current })
			.Skip(1) // ignore first value where there is no previous
			.ObserveOn(ctrl).Subscribe(v => {
				if (v.prev != v.current) {
					
					var pos = new Point(
						getRelativePosition(ctrl.Left, v.prev.X, v.current.X),
						getRelativePosition(ctrl.Top , v.prev.Y, v.current.Y)
					);
					if (pos != ctrl.Location) {
						ctrl.Location = pos;
						ctrl.Parent.Refresh();
					}
				}
			}, cts.Token);
			return cts;
		}
	}
}
