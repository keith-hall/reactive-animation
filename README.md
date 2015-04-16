# Reactive-Animation
Small, simple C# animation library built using the reactive extensions framework, utilizing Robert Penner's easing functions. Does not make use of reflection, instead allowing the caller to use a function/monad/observer to update their desired object.

Available as a NuGet package at https://www.nuget.org/packages/ReactiveAnimation/

## Why use reactive extensions?
Observables are very useful, because it allows you to easily react to events like resizes or repositions etc.  For example, you can animate an object to chase another object that is being animated, and have the animations scale automatically when the Form they are on changes size.
All the hard work is being done on separate threads, so you don't have to worry about it.  For Windows Forms, Rx can ensure that your subscribed observer executes on the Control thread, so it's super easy to update properties etc. without worrying about using Invoke.

## Principles
- The Animation sets the duration and (optionally) the easing function.
- Then you specify what values you want to animate, which you then subscribe to, essentially registering an observer that will update the desired object.
- Then you start the animation.
- Animations can be paused, cancelled, or skipped to completion etc.

## Examples
- Create an Animation that will last for 3 seconds and ease in and out using the quadratic curve
```cs
var a = new Animation {
  DurationInFrames = Animation.FromTimeSpanToDurationInFrames(3),
  EasingFunction = ef => Easing.EaseInOut(ef, EasingType.Quadratic)
};
```
- Using this animation, register an observer to animate a float from 0.8 to 1.0 and use the value to set the opacity of a form
```cs
a.AnimateOnControlThread(
  form, 
  ObservableHelper.FixedValue((float)0.8),
  ObservableHelper.FixedValue((float)1),
  v => f.Opacity = v.CurrentValue
);
```
- Start the animation
```cs
a.Start();
```

[![MyGet Build Status](https://www.myget.org/BuildSource/Badge/progamer-me?identifier=e8f7d0bd-e97a-4f4d-be10-e3d80f613a26)](https://www.myget.org/)
