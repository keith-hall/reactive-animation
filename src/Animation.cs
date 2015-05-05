using System;
using System.Collections.Generic;
using System.Linq;
//using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using AnimatableValue = System.Collections.Generic.KeyValuePair<string, float>;

namespace ReactiveAnimation
{
	public class Animation : IDisposable
	{
		public const int FrameRate = 60;

		public static readonly IObservable<long> EveryFrame = Observable.Interval(TimeSpan.FromMilliseconds((double)1000 / (double)FrameRate)) // create a cold observable
																		.Publish().RefCount(); // only pulse while subscribers are connected
		/// <summary>
		/// Convert the given <paramref name="timespan"/> to a duration in frames, based on the framerate.
		/// </summary>
		/// <param name="timespan">The duration to convert to frames.</param>
		/// <returns>The number of frames in the specified <paramref name="timespan"/>.</returns>
		public static int FromTimeSpanToDurationInFrames(TimeSpan timespan)
		{
			return FromTimeSpanToDurationInFrames(timespan.TotalSeconds);
		}
		
		/// <summary>
		/// Convert the given number of <paramref name="seconds"/> to a duration in frames, based on the framerate.
		/// </summary>
		/// <param name="seconds">The duration to convert to frames.</param>
		/// <returns>The number of frames in the specified <paramref name="seconds"/>.</returns>
		public static int FromTimeSpanToDurationInFrames(double seconds)
		{
			return (int)(seconds * FrameRate);
		}


		internal int _elapsedFrames = 0;
		private int _durationInFrames = FrameRate; // default to a duration of 1 second

		public int DurationInFrames
		{
			get
			{
				return _durationInFrames;
			}
			set
			{
				if (value < 1)
					throw new ArgumentOutOfRangeException("Duration", "Duration cannot be less than one frame");
				else if (value < _elapsedFrames)
					throw new ArgumentOutOfRangeException("Duration", "Duration cannot be less than elapsed frames.  If you want a shorter duration, you will need to restart the animation.");
				else
					_durationInFrames = value;
			}
		}

		internal Subject<float> _progress;

		public IObservable<float> Progress
		{
			get
			{
				return _progress.AsObservable(); // best to hide the identity of a subject, because we don't need to expose state information
			}
		}
		public Func<double, float> EasingFunction { get; set; }
		internal CancellationTokenSource _cancelProgress;
		//x internal List<CancellationTokenSource> _cancelChildren;

		public bool IsRunning
		{
			get
			{
				return _cancelProgress != null && !_cancelProgress.IsCancellationRequested;
			}
		}

		public Animation()
		{
			_progress = new Subject<float>();
			//x _cancelChildren = new List<CancellationTokenSource>();
			Pause();
		}

		public void Start()
		{
			Pause(); // ensure the animation is not running
			_cancelProgress = new CancellationTokenSource(); // create a new cancellation token source, so we can pause or complete the animation

			if (EasingFunction == null) // if there is no easing function specified, use the default linear type
				EasingFunction = (progress) => Easing.EaseInOut(progress, EasingType.Linear);

			try
			{
				EveryFrame.Subscribe(onNext =>
									 {
										 if (_elapsedFrames == DurationInFrames)
										 { // if the animation is complete, cancel it to break out of this subscription to EveryFrame
											 //? NOTE: we actually do this on the next frame to ensure the subscribers have had time to process the completion... maybe this is a HACK: ?
											 Pause();
											 CleanUp();
											 return;
										 }
										 _cancelProgress.Token.ThrowIfCancellationRequested();

										 _elapsedFrames++;

										 UpdateProgress();
									 },
					//x onError => onError.Dump("Animation Start -> onError"),
									 _cancelProgress.Token);
			}
			catch (InvalidOperationException ex)
			{
				// Invoke or BeginInvoke cannot be called on a control until the window handle has been created. [Source = Pooled Thread]
				// TODO: better to get subscribers to unsubscribe when form is being closed?
				//ex.Dump("Animation Start -> Caught error");
				//CleanUp();
			}
		}

		internal void UpdateProgress()
		{
			var percentComplete = (double)_elapsedFrames / (double)DurationInFrames; // determine the percent complete of the animation
			_progress.OnNext(EasingFunction(percentComplete)); // apply the easing function to the progress percentage, and report the eased value to any subscribers

			if (_elapsedFrames == DurationInFrames)
				_progress.OnCompleted(); // report the completion of this animation to any subscribers
		}

		public void Pause()
		{
			if (_cancelProgress != null)
				_cancelProgress.Cancel();
		}

		public void Restart()
		{
			Pause();
			GoToSpecificFrame(0);
			Start();
		}

		public void GoToSpecificFrame(int frameNumber)
		{
			if (frameNumber < 0 || frameNumber > DurationInFrames)
				throw new ArgumentOutOfRangeException("frameNumber");
			//? TODO: enforce that the animation is paused first? else may have threading issues whereby the elapsed frames is greater than the duration?

			_elapsedFrames = frameNumber;
			UpdateProgress();
		}

		public void SkipToCompletion()
		{
			Pause();
			GoToSpecificFrame(DurationInFrames);
		}

		public struct AnimationProgress
		{
			public string Key { get; internal set; }
			public float Progress { get; internal set; }
			public float FromValue { get; internal set; }
			public float ToValue { get; internal set; }

			public float CurrentValue
			{
				get
				{
					return FromValue + (ToValue - FromValue) * Progress;
				}
			}
		}

		/// <summary>
		/// create an observable for animating multiple related values at once
		/// </summary>
		/// <param name="fromValues">the observable values to animate from</param>
		/// <param name="toValues">the observable values to animate to</param>
		/// <returns>observable animation progress for subscription and updating the target object</returns>
		public IObservable<IEnumerable<AnimationProgress>> CreateObservable(IObservable<IEnumerable<AnimatableValue>> fromValues, IObservable<IEnumerable<AnimatableValue>> toValues)
		{
			return Progress.CombineLatest(fromValues.DistinctUntilChanged(), toValues.DistinctUntilChanged(),
				(p, f, t) => f.Join(t, fv => fv.Key, tv => tv.Key,
					(fv, tv) => new AnimationProgress
					{
						Key = fv.Key,
						FromValue = fv.Value,
						ToValue = tv.Value,
						Progress = p
					}));
		}


		/// <summary>
		/// create an observable for animating a single value
		/// </summary>
		/// <param name="fromValue">the observable value to animate from</param>
		/// <param name="toValue">the observable value to animate to</param>
		/// <param name="valueName">the name you want to assign the value</param>
		/// <returns>observable animation progress for subscription and updating the target object</returns>
		public IObservable<IEnumerable<AnimationProgress>> CreateObservable(IObservable<float> fromValue, IObservable<float> toValue, string valueName = "Value")
		{
			Func<float, IEnumerable<AnimatableValue>> conv = f => Enumerable.Repeat(new AnimatableValue(valueName, f), 1);
			return CreateObservable(fromValue.Select(conv), toValue.Select(conv));
		}

		/*// commented out because subscription automatically ends upon completion
		/// <summary>
		/// subscribe to an observable, and cancel the subscription automatically when the animation completes
		/// </summary>
		/// <param name="animation">the observable animation to subscribe to</param>
		/// <param name="onNext">the action to perform on each frame</param>
		/// <returns>a cancellation token, so that it can be cancelled separately from other animations controlled by this Animation</returns>
		internal CancellationTokenSource SubscribeDuringAnimation (IObservable<IEnumerable<AnimationProgress>> animation, Action<IEnumerable<AnimationProgress>> onNext)
		{
			var cts = new CancellationTokenSource();
			_cancelChildren.Add(cts);

			animation.Subscribe(onNext, cts.Token);

			return cts;
		}*/

		public static IEnumerable<AnimatableValue> ConvertRectangleToEnumerable(Rectangle rect)
		{
			Func<string, int, AnimatableValue> kvp = (s, i) => new AnimatableValue(s, i);

			return new[]
				   {
					   kvp("Left", rect.Left),
					   kvp("Top", rect.Top),
					   kvp("Width", rect.Width),
					   kvp("Height", rect.Height)
				   };
		}

		public static Rectangle ConvertEnumerableToRectangle(IEnumerable<AnimationProgress> rectEnumerable)
		{
			var coords = rectEnumerable.ToArray();

			//x return new Rectangle((int)coords[0].CurrentValue, (int)coords[1].CurrentValue, (int)coords[2].CurrentValue, (int)coords[3].CurrentValue);
			return new Rectangle((int)coords.Single(ap => ap.Key == "Left").CurrentValue, (int)coords.Single(ap => ap.Key == "Top").CurrentValue, (int)coords.Single(ap => ap.Key == "Width").CurrentValue, (int)coords.Single(ap => ap.Key == "Height").CurrentValue);
		}

		/// <summary>
		/// animate a Windows Forms Control, automatically subscribing and observing on the control's thread
		/// </summary>
		/// <param name="ctrl">the Windows Forms Control to animate</param>
		/// <param name="fromPosition">the starting position</param>
		/// <param name="toPosition">the ending position</param>
		/// <param name="onCompletion">an action to run on completion of the animation</param>
		public void AnimateControlPosition(Control ctrl, IObservable<Rectangle> fromPosition, IObservable<Rectangle> toPosition, Action onCompletion = null)
		{
			AnimateOnControlThread(ctrl, CreateObservable(fromPosition.Select(ConvertRectangleToEnumerable), toPosition.Select(ConvertRectangleToEnumerable)), eap => UpdateControlPosition(ctrl, ConvertEnumerableToRectangle(eap)), onCompletion);
		}

		// static
		public void AnimateOnControlThread(Control ctrl, IObservable<IEnumerable<AnimationProgress>> animationObservable, Action<IEnumerable<AnimationProgress>> onNext, Action onCompletion = null)
		{
			Action completion = () =>
			{
				if (onCompletion != null)
					ctrl.Invoke(onCompletion);
			};
			animationObservable
				.ObserveOn(ctrl)
				.Subscribe(onNext, completion);
		}

		public void AnimateOnControlThread(Control ctrl, IObservable<float> fromValue, IObservable<float> toValue, Action<AnimationProgress> onNext, Action onCompletion = null)
		{
			AnimateOnControlThread(ctrl, CreateObservable(fromValue, toValue), eap => onNext(eap.First()), onCompletion);
		}

		public static void UpdateControlPosition(Control ctrl, Rectangle newPosition)
		{
			ctrl.SetBounds(newPosition.Left, newPosition.Top, newPosition.Width, newPosition.Height);
			ctrl.Parent.Refresh();
		}

		public void Dispose()
		{
			CleanUp();
		}

		private void CleanUp()
		{
			Pause();
			//x foreach (var cts in _cancelChildren)
			//x 	cts.Cancel();
			_progress.Dispose();
		}
	}
}
