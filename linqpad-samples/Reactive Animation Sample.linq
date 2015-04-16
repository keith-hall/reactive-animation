<Query Kind="Program">
  <NuGetReference>Rx-Main</NuGetReference>
  <NuGetReference>Rx-WinForms</NuGetReference>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Windows.Forms</Namespace>
  <Namespace>System.Drawing</Namespace>
  <Namespace>System.Reactive.Concurrency</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>ReactiveAnimation</Namespace>
</Query>
void Main() // LINQPad sample
{
	var a = new Animation { DurationInFrames = Animation.FromTimeSpanToDurationInFrames(3), EasingFunction = ef => Easing.EaseInOut(ef, EasingType.Quadratic) };
	var f = new Form { Width = 600, Height = 500 };
	var b = new Button { Text = "Hello", Width = 100, Height = 50, Top = 50, Visible = true };
	var b2 = new Button { Text = "World", Width = 100, Height = 50, Top = 400, Visible = true };
	f.Controls.Add(b);
	f.Controls.Add(b2);
	f.Show();
	f.Update();
	f.Refresh();
	f.Focus();
	f.FormClosing += (o, ev) => { a.Dispose(); Environment.Exit(0); };
	
	var newPosForB = ObservableHelper.PositionBasedOnParent(b, c => new Rectangle(c.Parent.ClientSize.Width - c.Width, c.Top, c.Width, c.Height));
	var newPosForB2 = ObservableHelper.PositionBasedOnControl(b, c => new Rectangle(c.Left, c.Top + c.Height, b2.Width, b2.Height));
	//newPosForB.DumpLatest(true); // commented out due to LINQPad not focusing the form when there are dumped objects
	//newPosForB2.Dump();
	a.AnimateControlPosition(b, ObservableHelper.FixedValue(b.Bounds), newPosForB);
	a.AnimateControlPosition(b2, ObservableHelper.FixedPositionRelativeToParent(b2).Select(p => new Rectangle(p.X, p.Y, b2.Width, b2.Height)), newPosForB2);
	
	a.AnimateOnControlThread(f, ObservableHelper.FixedValue((float)0.8), ObservableHelper.FixedValue((float)1), v => f.Opacity = v.CurrentValue);
	
	a.Progress.Subscribe(v => {}, () => {
		b.Invoke(() => {
		    b.Text = "Complete!";
			var ct = new CancellationTokenSource();
			newPosForB.Subscribe(b.SetBounds, ct.Token);
			newPosForB2.Subscribe(b2.SetBounds, ct.Token);
			b.Disposed += (o, ef) => ct.Cancel();
		});
	});
	a.Start();
	
	new Task(() => {
			Thread.Sleep(1500);
			b2.Invoke(() => b2.Text = "Paused...");
			a.Pause();
			//f.Invoke(() => f.Width = (int)((double)f.Width * 1.5));
			//f.Invoke(() => f.WindowState = FormWindowState.Maximized); // resize the form to show the animation is also updated
			Thread.Sleep(1000);
			b2.Invoke(() => b2.Text = "World");
			a.Start();
	}).Start();
	
	b2.Click += (o, e) => {
		var a2 = new Animation { DurationInFrames = Animation.FromTimeSpanToDurationInFrames(0.2), EasingFunction = v => Easing.EaseOut(v, EasingType.Sine) };
		// showing a different way of doing it
		a2.CreateObservable(ObservableHelper.FixedValue((float)f.Width), ObservableHelper.FixedValue((float)f.Width * (float)1.2)).ObserveOn(f).Subscribe(v => f.Width = (int)v.First().CurrentValue);
		a2.Start();
	};
	b.Click += (o, e) => {
		var a2 = new Animation { DurationInFrames = Animation.FromTimeSpanToDurationInFrames(1), EasingFunction = v => Easing.EaseOut(v, EasingType.Linear) };
		var d1 = new Dictionary<string, float>();
		d1.Add("R", 127);
		d1.Add("G", 255);
		d1.Add("B", -255);
		var d2 = new Dictionary<string, float>();
		d2.Add("R", 0);
		d2.Add("G", -127);
		d2.Add("B", 255);
		
		a2.AnimateOnControlThread(b, a2.CreateObservable(ObservableHelper.FixedValue(d1), ObservableHelper.FixedValue(d2))/*.Dump()*/, eap => b.BackColor = Color.FromArgb((int)Math.Abs(eap.Single (p => p.Key == "R").CurrentValue), (int)Math.Abs(eap.Single (p => p.Key == "G").CurrentValue), (int)Math.Abs(eap.Single (p => p.Key == "B").CurrentValue)), () => b.ForeColor = Color.White);
		a2.Start();
	};
}

public static class ControlExtensions {
    public static void Invoke (this Control control, Action action) {
		control.Invoke(action);
	}
	
	public static void SetBounds (this Control control, Rectangle newBounds) {
		control.SetBounds(newBounds.Left, newBounds.Top, newBounds.Width, newBounds.Height);
	}
}
