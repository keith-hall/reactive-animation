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
		private static IObservable<Rectangle> PositionBasedOnControl<TEventArgs>(IObservable<EventPattern<object, TEventArgs>> events, Control ctrl, Func<Control, Rectangle> getNewPosition) where TEventArgs : EventArgs
		{
			return Observable.Defer(() => Observable.Repeat(getNewPosition(ctrl), 1)).Concat(events.Select(e => getNewPosition(ctrl)).ObserveOn(ctrl));
		}

		public static IObservable<Rectangle> PositionBasedOnControl(Control ctrlDestination, Func<Control, Rectangle> getNewPosition)
		{
			return PositionBasedOnControl(Observable.FromEventPattern(ev => ctrlDestination.Move += ev, ev => { if (ctrlDestination != null && !ctrlDestination.IsDisposed) ctrlDestination.Move -= ev; }), ctrlDestination, getNewPosition);
		}

		public static IObservable<Rectangle> PositionBasedOnParent(Control ctrl, Func<Control, Rectangle> getNewPosition)
		{
			return PositionBasedOnControl(Observable.FromEventPattern(ev => ctrl.Parent.ClientSizeChanged += ev, ev => { if (ctrl != null && !ctrl.IsDisposed) ctrl.Parent.ClientSizeChanged -= ev; }), ctrl, getNewPosition);
		}

		/// <summary>
		/// convert a value to an observable that will never change
		/// </summary>
		/// <param name="constantValue">the fixed value</param>
		/// <returns>an observable with the constant value specified</returns>
		public static IObservable<T> FixedValue<T>(T constantValue)
		{
			return Enumerable.Repeat(constantValue, 1).ToObservable();
		}

		/// <summary>
		/// get an observable rectangle that will contain a control's position relative to it's parent
		/// </summary>
		/// <param name="ctrl">the control</param>
		/// <returns>an observable with the position relative to it's parent</returns>
		public static IObservable<Rectangle> FixedPositionRelativeToParent(Control ctrl)
		{
			var ps = ctrl.Parent.ClientSize;
			var originalXPerc = (float)ctrl.Left / (float)ps.Width;
			var originalYPerc = (float)ctrl.Top / (float)ps.Height;
			return PositionBasedOnParent(ctrl, c => new Rectangle((int)((float)c.Parent.ClientSize.Width * originalXPerc), (int)((float)c.Parent.ClientSize.Height * originalYPerc), c.Width, c.Height));
		}
		
		/// <summary>
		/// get an observable coupled with it's previous value
		/// </summary>
		/// <param name="source">the source observable</param>
		/// <param name="projection">the projection to apply to get the output</param>
		/// <returns>an observable coupled with it's previous value</returns>
		public static IObservable<TOutput> ObserveWithPrevious<TSource, TOutput> (IObservable<TSource> source, Func<TSource, TSource, TOutput> projection) {
			return source.Scan(Tuple.Create(default(TSource), default(TSource)),
				(previous, current) => projection(previous.Item2, current));
		}
	}
}
