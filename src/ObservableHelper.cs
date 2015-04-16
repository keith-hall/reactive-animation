using System;
using System.Drawing;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Forms;

namespace ReactiveAnimation
{
	public static class ObservableHelper
	{
		private static IObservable<T> PositionBasedOnControl<T>(IObservable<EventPattern<object>> events, Control ctrl, Func<Control, T> getNewPosition) 
		{
			return Observable.Defer(() => FixedValue(getNewPosition(ctrl))).Concat(events.Select(e => getNewPosition(ctrl)).ObserveOn(ctrl));
		}
		
		/// <summary>
		/// Get an observable that will update using the provided function when the specified <paramref name="ctrlDestination"/> control's position changes.
		/// </summary>
		/// <param name="ctrlDestination">The control to observe.</param>
		/// <returns>An observable with the position relative to <paramref name="ctrlDestination"/>.</returns>
		public static IObservable<T> PositionBasedOnControl<T>(Control ctrlDestination, Func<Control, T> getNewPosition)
		{
			return PositionBasedOnControl(Observable.FromEventPattern(ev => ctrlDestination.Move += ev, ev => { if (ctrlDestination != null && !ctrlDestination.IsDisposed) ctrlDestination.Move -= ev; }), ctrlDestination, getNewPosition);
		}
		
		/// <summary>
		/// Get an observable that will update using the provided function when the specified <paramref name="ctrl"/> control's parent's size changes.
		/// </summary>
		/// <param name="ctrl">The control whose parent to observe for size changes.</param>
		/// <returns>An observable with the position relative to the size of <paramref name="ctrl"/>'s parent.</returns>
		public static IObservable<T> PositionBasedOnParent<T>(Control ctrl, Func<Control, T> getNewPosition)
		{
			return PositionBasedOnControl(Observable.FromEventPattern(ev => ctrl.Parent.ClientSizeChanged += ev, ev => { if (ctrl != null && !ctrl.IsDisposed) ctrl.Parent.ClientSizeChanged -= ev; }), ctrl, getNewPosition);
		}

		/// <summary>
		/// Convert a value to an observable that will never change.
		/// </summary>
		/// <param name="constantValue">The fixed value.</param>
		/// <returns>An observable with the constant value specified.</returns>
		public static IObservable<T> FixedValue<T>(T constantValue)
		{
			return Enumerable.Repeat(constantValue, 1).ToObservable();
		}

		/// <summary>
		/// Get an observable Point that will contain a control's position relative to it's parent.
		/// </summary>
		/// <param name="ctrl">The control.</param>
		/// <returns>An observable with the position relative to the control's parent.</returns>
		public static IObservable<Point> FixedPositionRelativeToParent(Control ctrl)
		{
			var ps = ctrl.Parent.ClientSize;
			var originalXPerc = (float)ctrl.Left / (float)ps.Width;
			var originalYPerc = (float)ctrl.Top / (float)ps.Height;
			return PositionBasedOnParent(ctrl, c => new Point((int)((float)c.Parent.ClientSize.Width * originalXPerc), (int)((float)c.Parent.ClientSize.Height * originalYPerc)));
		}
		
		/// <summary>
		/// Get an observable coupled with it's previous value.
		/// </summary>
		/// <param name="source">The source observable.</param>
		/// <param name="projection">The projection to apply to get the output.</param>
		/// <returns>An observable coupled with it's previous value.</returns>
		public static IObservable<TOutput> ObserveWithPrevious<TSource, TOutput> (IObservable<TSource> source, Func<TSource, TSource, TOutput> projection) {
			return source.Scan(Tuple.Create(default(TSource), default(TSource)),
			(previous, current) => Tuple.Create(previous.Item2, current))
				.Select(t => projection(t.Item1, t.Item2));
		}
	}
}
