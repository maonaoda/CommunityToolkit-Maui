using CommunityToolkit.Maui.Core;
using CoreGraphics;
using Foundation;
using UIKit;

namespace CommunityToolkit.Maui.Behaviors;

public partial class TouchBehavior
{
	UIGestureRecognizer? touchGesture;
	UIGestureRecognizer? hoverGesture;

	/// <summary>
	/// Attaches the behavior to the platform view.
	/// </summary>
	/// <param name="bindable">Maui Visual Element</param>
	/// <param name="platformView">Native View</param>
	protected override void OnAttachedTo(VisualElement bindable, UIView platformView)
	{
		base.OnAttachedTo(bindable, platformView);

		Element = bindable;

		touchGesture = new TouchUITapGestureRecognizer(this);

		platformView.AddGestureRecognizer(touchGesture);

		if (OperatingSystem.IsIOSVersionAtLeast(13))
		{
			hoverGesture = new UIHoverGestureRecognizer(OnHover);
			platformView.AddGestureRecognizer(hoverGesture);
		}

		platformView.UserInteractionEnabled = true;
	}

	/// <summary>
	/// Detaches the behavior from the platform view.
	/// </summary>
	/// <param name="bindable">Maui Visual Element</param>
	/// <param name="platformView">Native View</param>
	protected override void OnDetachedFrom(VisualElement bindable, UIView platformView)
	{
		base.OnDetachedFrom(bindable, platformView);

		if (touchGesture is not null)
		{
			platformView.RemoveGestureRecognizer(touchGesture);
			touchGesture?.Dispose();
			touchGesture = null;
		}

		if (hoverGesture is not null)
		{
			platformView.RemoveGestureRecognizer(hoverGesture);
			hoverGesture?.Dispose();
			hoverGesture = null;
		}

		Element = null;
	}

	void OnHover()
	{
		if (!IsEnabled)
		{
			return;
		}

		switch (hoverGesture?.State)
		{
			case UIGestureRecognizerState.Began:
			case UIGestureRecognizerState.Changed:
				HandleHover(HoverStatus.Entered);
				break;
			case UIGestureRecognizerState.Ended:
				HandleHover(HoverStatus.Exited);
				break;
		}
	}

	partial void PlatformDispose()
	{
		touchGesture?.Dispose();
		touchGesture = null;

		hoverGesture?.Dispose();
		hoverGesture = null;
	}

	/// <summary>
	/// iOS/tvOS implementation of the Touch Gesture Recognizer.
	/// </summary>
	public class TouchUITapGestureRecognizer : UIGestureRecognizer
	{
		readonly TouchBehavior behavior;

		CGPoint? startPoint;

		bool isCanceled;

		/// <summary>
		/// Initializes a new instance of the <see cref="TouchUITapGestureRecognizer"/> class.
		/// </summary>
		/// <param name="behavior"></param>
		public TouchUITapGestureRecognizer(TouchBehavior behavior)
		{
			this.behavior = behavior;
			CancelsTouchesInView = false;
			Delegate = new TouchUITapGestureRecognizerDelegate();
		}

		/// <summary>
		/// Touches Began event handler.
		/// </summary>
		/// <param name="touches"></param>
		/// <param name="evt"></param>
		public override void TouchesBegan(NSSet touches, UIEvent evt)
		{
			base.TouchesBegan(touches, evt);

			if (!behavior.IsEnabled)
			{
				return;
			}

			isCanceled = false;
			startPoint = GetTouchPoint(touches);

			HandleTouch(TouchStatus.Started, TouchInteractionStatus.Started);
		}

		/// <summary>
		/// Touches Ended event handler.
		/// </summary>
		/// <param name="touches"></param>
		/// <param name="evt"></param>
		public override void TouchesEnded(NSSet touches, UIEvent evt)
		{
			base.TouchesEnded(touches, evt);

			if (!behavior.IsEnabled)
			{
				return;
			}

			HandleTouch(behavior.CurrentTouchStatus is TouchStatus.Started ? TouchStatus.Completed : TouchStatus.Canceled,
				TouchInteractionStatus.Completed);

			isCanceled = true;
		}

		/// <summary>
		/// Touches Cancelled event handler.
		/// </summary>
		/// <param name="touches"></param>
		/// <param name="evt"></param>
		public override void TouchesCancelled(NSSet touches, UIEvent evt)
		{
			base.TouchesCancelled(touches, evt);

			if (!behavior.IsEnabled)
			{
				return;
			}

			HandleTouch(TouchStatus.Canceled, TouchInteractionStatus.Completed);

			isCanceled = true;
		}

		/// <summary>
		/// Touches Moved event handler.
		/// </summary>
		/// <param name="touches"></param>
		/// <param name="evt"></param>
		public override void TouchesMoved(NSSet touches, UIEvent evt)
		{
			base.TouchesMoved(touches, evt);

			if (!behavior.IsEnabled)
			{
				return;
			}

			var disallowTouchThreshold = behavior.DisallowTouchThreshold;
			var point = GetTouchPoint(touches);
			if (point is not null && startPoint is not null && disallowTouchThreshold > 0)
			{
				var diffX = Math.Abs(point.Value.X - startPoint.Value.X);
				var diffY = Math.Abs(point.Value.Y - startPoint.Value.Y);
				var maxDiff = Math.Max(diffX, diffY);
				if (maxDiff > disallowTouchThreshold)
				{
					HandleTouch(TouchStatus.Canceled, TouchInteractionStatus.Completed);
					isCanceled = true;
					return;
				}
			}

			var status = point is not null && View.Bounds.Contains(point.Value)
				? TouchStatus.Started
				: TouchStatus.Canceled;

			if (behavior.CurrentTouchStatus != status)
			{
				HandleTouch(status);
			}

			if (status is TouchStatus.Canceled)
			{
				isCanceled = true;
			}
		}

		/// <summary>
		/// Disposes the gesture recognizer.
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				Delegate.Dispose();
			}

			base.Dispose(disposing);
		}

		void HandleTouch(TouchStatus status, TouchInteractionStatus? interactionStatus = null)
		{
			if (isCanceled || !behavior.IsEnabled)
			{
				return;
			}

			if (interactionStatus is TouchInteractionStatus.Started)
			{
				behavior.HandleUserInteraction(TouchInteractionStatus.Started);
				interactionStatus = null;
			}

			behavior.HandleTouch(status);

			if (interactionStatus.HasValue)
			{
				behavior.HandleUserInteraction(interactionStatus.Value);
			}
		}

		CGPoint? GetTouchPoint(NSSet touches)
		{
			return (touches?.AnyObject as UITouch)?.LocationInView(View);
		}

		/// <summary>
		/// iOS/tvOS implementation of the Touch Gesture Recognizer Delegate.
		/// </summary>
		public class TouchUITapGestureRecognizerDelegate : UIGestureRecognizerDelegate
		{
			/// <summary>
			/// Determines whether multiple gesture recognizers should be allowed to recognize gestures simultaneously.
			/// </summary>
			/// <param name="gestureRecognizer"></param>
			/// <param name="otherGestureRecognizer"></param>
			/// <returns></returns>
			public override bool ShouldRecognizeSimultaneously(UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer)
			{
				if (gestureRecognizer is TouchUITapGestureRecognizer touchGesture && otherGestureRecognizer is UIPanGestureRecognizer &&
					otherGestureRecognizer.State is UIGestureRecognizerState.Began)
				{
					touchGesture.HandleTouch(TouchStatus.Canceled, TouchInteractionStatus.Completed);
					touchGesture.isCanceled = true;
				}

				return true;
			}

			/// <summary>
			/// Determines whether the gesture recognizer should receive the touch.
			/// </summary>
			/// <param name="recognizer"></param>
			/// <param name="touch"></param>
			/// <returns></returns>
			public override bool ShouldReceiveTouch(UIGestureRecognizer recognizer, UITouch touch)
			{
				if (recognizer.View.IsDescendantOfView(touch.View))
				{
					return true;
				}

				return touch.View.IsDescendantOfView(recognizer.View) && (touch.View.GestureRecognizers is null || touch.View.GestureRecognizers.Length == 0);
			}
		}
	}
}