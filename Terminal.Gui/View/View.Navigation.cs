using System.Diagnostics;
using static Terminal.Gui.FakeDriver;

namespace Terminal.Gui;

public partial class View // Focus and cross-view navigation management (TabStop, TabIndex, etc...)
{

    private NavigationDirection _focusDirection;

    /// <summary>
    ///     Advances the focus to the next or previous view in <see cref="View.TabIndexes"/>, based on
    ///     <paramref name="direction"/>.
    ///     itself.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         If there is no next/previous view, the focus is set to the view itself.
    ///     </para>
    /// </remarks>
    /// <param name="direction"></param>
    /// <param name="groupOnly">If <see langword="true"/> will advance into ...</param>
    /// <returns>
    ///     <see langword="true"/> if focus was changed to another subview (or stayed on this one), <see langword="false"/>
    ///     otherwise.
    /// </returns>
    public bool AdvanceFocus (NavigationDirection direction, TabBehavior? behavior)
    {
        if (!CanBeVisible (this))
        {
            return false;
        }

        FocusDirection = direction;

        if (TabIndexes is null || TabIndexes.Count == 0)
        {
            return false;
        }

        if (Focused is null)
        {
            FocusDeepest (behavior, direction);

            return Focused is { };
        }

        if (Focused is { })
        {
            if (Focused.AdvanceFocus (direction, behavior))
            {
                return true;
            }
        }

        var index = GetScopedTabIndexes (behavior, direction);

        if (index.Length == 0)
        {
            return false;
        }

        var focusedIndex = index.IndexOf (Focused);
        int next = 0;

        if (focusedIndex < index.Length - 1)
        {
            next = focusedIndex + 1;
        }
        else
        {
            if (behavior == TabBehavior.TabGroup && behavior == TabStop && SuperView?.TabStop == TabBehavior.TabGroup)
            {
                // Go up the hierarchy
                // Leave
                Focused.SetHasFocus (false, this);

                // Signal that nothing is focused, and callers should try a peer-subview
                Focused = null;

                return false;
            }

            //Focused.RestoreFocus (TabBehavior.TabStop);

            //if (Focused is { })
            //{
            //    return true;
            //}

            // Wrap around
            //if (SuperView is {})
            //{
            //    if (direction == NavigationDirection.Forward)
            //    {
            //        return false;
            //    }
            //    else
            //    {
            //        return false;

            //        //SuperView.FocusFirst (groupOnly);
            //    }
            //    return true;
            //}
            //next = index.Length - 1;

        }

        View view = index [next];

        if (view.HasFocus)
        {
            return true;
        }

        // The subview does not have focus, but at least one other that can. Can this one be focused?
        if (view.CanFocus && view.Visible && view.Enabled)
        {
            // Make Focused Leave
            Focused.SetHasFocus (false, view);

            view.FocusDeepest (TabBehavior.TabStop, direction);

            return true;
        }

        if (Focused is { })
        {
            // Leave
            Focused.SetHasFocus (false, this);

            // Signal that nothing is focused, and callers should try a peer-subview
            Focused = null;
        }

        return false;
    }

#if AUTO_CANFOCUS
    // BUGBUG: This is a poor API design. Automatic behavior like this is non-obvious and should be avoided. Instead, callers to Add should be explicit about what they want.
    // Set to true in Add() to indicate that the view being added to a SuperView has CanFocus=true.
    // Makes it so CanFocus will update the SuperView's CanFocus property.
    internal bool _addingViewSoCanFocusAlsoUpdatesSuperView;

    // Used to cache CanFocus on subviews when CanFocus is set to false so that it can be restored when CanFocus is changed back to true
    private bool _oldCanFocus;
#endif

    private bool _canFocus;

    /// <summary>Gets or sets a value indicating whether this <see cref="View"/> can be focused.</summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="SuperView"/> must also have <see cref="CanFocus"/> set to <see langword="true"/>.
    ///     </para>
    ///     <para>
    ///         When set to <see langword="false"/>, if an attempt is made to make this view focused, the focus will be set to
    ///         the next focusable view.
    ///     </para>
    ///     <para>
    ///         When set to <see langword="false"/>, the <see cref="TabIndex"/> will be set to -1.
    ///     </para>
    ///     <para>
    ///         When set to <see langword="false"/>, the values of <see cref="CanFocus"/> and <see cref="TabIndex"/> for all
    ///         subviews will be cached so that when <see cref="CanFocus"/> is set back to <see langword="true"/>, the subviews
    ///         will be restored to their previous values.
    ///     </para>
    ///     <para>
    ///         Changing this property to <see langword="true"/> will cause <see cref="TabStop"/> to be set to
    ///         <see cref="TabBehavior.TabStop"/>" as a convenience. Changing this property to
    ///         <see langword="false"/> will have no effect on <see cref="TabStop"/>.
    ///     </para>
    /// </remarks>
    public bool CanFocus
    {
        get => _canFocus;
        set
        {
#if AUTO_CANFOCUS
            if (!_addingViewSoCanFocusAlsoUpdatesSuperView && IsInitialized && SuperView?.CanFocus == false && value)
            {
                throw new InvalidOperationException ("Cannot set CanFocus to true if the SuperView CanFocus is false!");
            }
#endif

            if (_canFocus == value)
            {
                return;
            }

            _canFocus = value;

#if AUTO_CANFOCUS
            switch (_canFocus)
            {
                case false when _tabIndex > -1:
                    // BUGBUG: This is a poor API design. Automatic behavior like this is non-obvious and should be avoided. Callers should adjust TabIndex explicitly.
                    //TabIndex = -1;

                    break;

                case true when SuperView?.CanFocus == false && _addingViewSoCanFocusAlsoUpdatesSuperView:
                    SuperView.CanFocus = true;

                    break;
            }
#endif

            if (TabStop is null && _canFocus)
            {
                TabStop = TabBehavior.TabStop;
            }

            if (!_canFocus && SuperView?.Focused == this)
            {
                SuperView.Focused = null;
            }

            if (!_canFocus && HasFocus)
            {
                SetHasFocus (false, this);
                SuperView?.RestoreFocus (null);

                // If EnsureFocus () didn't set focus to a view, focus the next focusable view in the application
                if (SuperView is { Focused: null })
                {
                    SuperView.AdvanceFocus (NavigationDirection.Forward, null);

                    if (SuperView.Focused is null && Application.Current is { })
                    {
                        Application.Current.AdvanceFocus (NavigationDirection.Forward, null);
                    }

                    ApplicationOverlapped.BringOverlappedTopToFront ();
                }
            }

            if (_subviews is { } && IsInitialized)
            {
#if AUTO_CANFOCUS
                // Change the CanFocus of all subviews to the same value as this view
                // if the CanFocus of the subview is different from the value being set
                foreach (View view in _subviews)
                {
                    if (view.CanFocus != value)
                    {
                        if (!value)
                        {
                            // Cache the old CanFocus and TabIndex so that they can be restored when CanFocus is changed back to true
                            view._oldCanFocus = view.CanFocus;
                            view._oldTabIndex = view._tabIndex;
                            view.CanFocus = false;

                            //view._tabIndex = -1;
                        }
                        else
                        {
                            if (_addingViewSoCanFocusAlsoUpdatesSuperView)
                            {
                                view._addingViewSoCanFocusAlsoUpdatesSuperView = true;
                            }

                            // Restore the old CanFocus and TabIndex to the values they held before CanFocus was set to false
                            view.CanFocus = view._oldCanFocus;
                            view._tabIndex = view._oldTabIndex;
                            view._addingViewSoCanFocusAlsoUpdatesSuperView = false;
                        }
                    }
                }
#endif
                if (this is Toplevel && Application.Current!.Focused != this)
                {
                    ApplicationOverlapped.BringOverlappedTopToFront ();
                }
            }

            OnCanFocusChanged ();
            SetNeedsDisplay ();
        }
    }

    /// <summary>Raised when <see cref="CanFocus"/> has been changed.</summary>
    /// <remarks>
    ///     Raised by the <see cref="OnCanFocusChanged"/> virtual method.
    /// </remarks>
    public event EventHandler CanFocusChanged;

    /// <summary>Raised when the view is gaining (entering) focus. Can be cancelled.</summary>
    /// <remarks>
    ///     Raised by the <see cref="OnEnter"/> virtual method.
    /// </remarks>
    public event EventHandler<FocusEventArgs> Enter;

    /// <summary>Returns the currently focused Subview inside this view, or <see langword="null"/> if nothing is focused.</summary>
    /// <value>The currently focused Subview.</value>
    public View Focused { get; private set; }

    /// <summary>
    ///     Focuses the deepest focusable view in <see cref="View.TabIndexes"/> if one exists. If there are no views in
    ///     <see cref="View.TabIndexes"/> then the focus is set to the view itself.
    /// </summary>
    /// <param name="behavior"></param>
    /// <param name="direction"></param>
    public void FocusDeepest (TabBehavior? behavior, NavigationDirection direction)
    {
        if (!CanBeVisible (this))
        {
            return;
        }

        //View deepest = FindDeepestFocusableView (behavior, direction);

        //if (deepest is { })
        //{
        //    deepest.SetFocus ();
        //}

        if (_tabIndexes is null)
        {
            SuperView?.SetFocus (this);

            return;
        }

        SetFocus ();

        foreach (View view in _tabIndexes)
        {
            if (view.CanFocus && (behavior is null || view.TabStop == behavior) && view.Visible && view.Enabled)
            {
                SetFocus (view);

                return;
            }
        }
    }

    [CanBeNull]
    private View FindDeepestFocusableView (TabBehavior? behavior, NavigationDirection direction)
    {
        var indicies = GetScopedTabIndexes (behavior, direction);

        foreach (View v in indicies)
        {
            if (v.TabIndexes.Count == 0)
            {
                return v;
            }

            return v.FindDeepestFocusableView (behavior, direction);
        }

        return null;
    }

    private bool _hasFocus;

    /// <summary>
    ///     Gets or sets whether this view has focus.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Causes the <see cref="OnEnter"/> and <see cref="OnLeave"/> virtual methods (and <see cref="Enter"/> and
    ///         <see cref="Leave"/> events to be raised) when the value changes.
    ///     </para>
    ///     <para>
    ///         Setting this property to <see langword="false"/> will recursively set <see cref="HasFocus"/> to
    ///         <see langword="false"/>
    ///         for any focused subviews.
    ///     </para>
    /// </remarks>
    public bool HasFocus
    {
        // Force the specified view to have focus
        set => SetHasFocus (value, this, true);
        get => _hasFocus;
    }

    /// <summary>Returns a value indicating if this View is currently on Top (Active)</summary>
    public bool IsCurrentTop => Application.Current == this;

    /// <summary>Raised when the view is losing (leaving) focus. Can be cancelled.</summary>
    /// <remarks>
    ///     Raised by the <see cref="OnLeave"/> virtual method.
    /// </remarks>
    public event EventHandler<FocusEventArgs> Leave;

    /// <summary>
    ///     Returns the most focused Subview in the chain of subviews (the leaf view that has the focus), or
    ///     <see langword="null"/> if nothing is focused.
    /// </summary>
    /// <value>The most focused Subview.</value>
    public View MostFocused
    {
        get
        {
            if (Focused is null)
            {
                return null;
            }

            View most = Focused.MostFocused;

            if (most is { })
            {
                return most;
            }

            return Focused;
        }
    }

    /// <summary>Invoked when the <see cref="CanFocus"/> property from a view is changed.</summary>
    /// <remarks>
    ///     Raises the <see cref="CanFocusChanged"/> event.
    /// </remarks>
    public virtual void OnCanFocusChanged () { CanFocusChanged?.Invoke (this, EventArgs.Empty); }

    // BUGBUG: The focus API is poorly defined and implemented. It deeply intertwines the view hierarchy with the tab order.

    /// <summary>Invoked when this view is gaining focus (entering).</summary>
    /// <param name="leavingView">The view that is leaving focus.</param>
    /// <returns> <see langword="true"/>, if the event was handled, <see langword="false"/> otherwise.</returns>
    /// <remarks>
    ///     <para>
    ///         Overrides must call the base class method to ensure that the <see cref="Enter"/> event is raised. If the event
    ///         is handled, the method should return <see langword="true"/>.
    ///     </para>
    /// </remarks>
    public virtual bool OnEnter (View leavingView)
    {
        // BUGBUG: _hasFocus should ALWAYS be false when this method is called. 
        if (_hasFocus)
        {
            Debug.WriteLine ($"BUGBUG: HasFocus should be false when OnEnter is called - Leaving: {leavingView} Entering: {this}");

            // return true;
        }

        var args = new FocusEventArgs (leavingView, this);
        Enter?.Invoke (this, args);

        if (args.Handled)
        {
            return true;
        }

        return false;
    }

    /// <summary>Invoked when this view is losing focus (leaving).</summary>
    /// <param name="enteringView">The view that is entering focus.</param>
    /// <returns> <see langword="true"/>, if the event was handled, <see langword="false"/> otherwise.</returns>
    /// <remarks>
    ///     <para>
    ///         Overrides must call the base class method to ensure that the <see cref="Leave"/> event is raised. If the event
    ///         is handled, the method should return <see langword="true"/>.
    ///     </para>
    /// </remarks>
    public virtual bool OnLeave (View enteringView)
    {
        // BUGBUG: _hasFocus should ALWAYS be true when this method is called. 
        if (!_hasFocus)
        {
            Debug.WriteLine ($"BUGBUG: HasFocus should be true when OnLeave is called - Leaving: {this} Entering: {enteringView}");

            //return true;
        }

        var args = new FocusEventArgs (this, enteringView);
        Leave?.Invoke (this, args);

        if (args.Handled)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Causes this view to be focused. All focusable views up the Superview hierarchy will also be focused.
    /// </summary>
    public void SetFocus ()
    {
        if (!CanBeVisible (this) || !Enabled)
        {
            if (HasFocus)
            {
                // If this view is focused, make it leave focus
                SetHasFocus (false, this);
            }

            return;
        }

        // Recursively set focus upwards in the view hierarchy
        if (SuperView is { })
        {
            SuperView.SetFocus (this);
        }
        else
        {
            SetFocus (this);
        }
    }

    /// <summary>
    ///     INTERNAL API that gets or sets the focus direction for this view and all subviews.
    ///     Setting this property will set the focus direction for all views up the SuperView hierarchy.
    /// </summary>
    internal NavigationDirection FocusDirection
    {
        get => SuperView?.FocusDirection ?? _focusDirection;
        set
        {
            if (SuperView is { })
            {
                SuperView.FocusDirection = value;
            }
            else
            {
                _focusDirection = value;
            }
        }
    }

    /// <summary>
    ///     INTERNAL helper for calling <see cref="FocusDeepest"/> or <see cref="FocusLast"/> based on
    ///     <see cref="FocusDirection"/>.
    ///     FocusDirection is not public. This API is thus non-deterministic from a public API perspective.
    /// </summary>
    internal void RestoreFocus (TabBehavior? behavior)
    {
        if (Focused is null && _subviews?.Count > 0)
        {
            FocusDeepest (behavior, FocusDirection);
        }
    }

    /// <summary>
    ///     Internal API that causes <paramref name="viewToEnterFocus"/> to enter focus.
    ///     <paramref name="viewToEnterFocus"/> does not need to be a subview.
    ///     Recursively sets focus DOWN in the view hierarchy.
    /// </summary>
    /// <param name="viewToEnterFocus"></param>
    private void SetFocus (View viewToEnterFocus)
    {
        if (viewToEnterFocus is null)
        {
            return;
        }

        if (!viewToEnterFocus.CanFocus || !viewToEnterFocus.Visible || !viewToEnterFocus.Enabled)
        {
            return;
        }

        // If viewToEnterFocus is already the focused view, don't do anything
        if (Focused?._hasFocus == true && Focused == viewToEnterFocus)
        {
            return;
        }

        // If a subview has focus and viewToEnterFocus is the focused view's superview OR viewToEnterFocus is this view,
        // then make viewToEnterFocus.HasFocus = true and return
        if ((Focused?._hasFocus == true && Focused?.SuperView == viewToEnterFocus) || viewToEnterFocus == this)
        {
            if (!viewToEnterFocus._hasFocus)
            {
                viewToEnterFocus._hasFocus = true;
            }

            return;
        }

        // Make sure that viewToEnterFocus is a subview of this view
        View c;

        for (c = viewToEnterFocus._superView; c != null; c = c._superView)
        {
            if (c == this)
            {
                break;
            }
        }

        if (c is null)
        {
            throw new ArgumentException (@$"The specified view {viewToEnterFocus} is not part of the hierarchy of {this}.");
        }

        // If a subview has focus, make it leave focus. This will leave focus up the hierarchy.
        Focused?.SetHasFocus (false, viewToEnterFocus);

        // make viewToEnterFocus Focused and enter focus
        View f = Focused;
        Focused = viewToEnterFocus;
        Focused?.SetHasFocus (true, f, true);
        Focused?.FocusDeepest (null, NavigationDirection.Forward);

        // Recursively set focus down the view hierarchy
        if (SuperView is { })
        {
            SuperView.SetFocus (this);
        }
        else
        {
            // If there is no SuperView, then this is a top-level view
            SetFocus (this);
        }
    }

    /// <summary>
    ///     Internal API that sets <see cref="HasFocus"/>. This method is called by <c>HasFocus_set</c> and other methods that
    ///     need to set or remove focus from a view.
    /// </summary>
    /// <param name="newHasFocus">The new setting for <see cref="HasFocus"/>.</param>
    /// <param name="view">The view that will be gaining or losing focus.</param>
    /// <param name="force">
    ///     <see langword="true"/> to force Enter/Leave on <paramref name="view"/> regardless of whether it
    ///     already HasFocus or not.
    /// </param>
    /// <remarks>
    ///     If <paramref name="newHasFocus"/> is <see langword="false"/> and there is a focused subview (<see cref="Focused"/>
    ///     is not <see langword="null"/>),
    ///     this method will recursively remove focus from any focused subviews of <see cref="Focused"/>.
    /// </remarks>
    private void SetHasFocus (bool newHasFocus, View view, bool force = false)
    {
        if (HasFocus != newHasFocus || force)
        {
            _hasFocus = newHasFocus;

            if (newHasFocus)
            {
                Debug.Assert (view is null || SuperView is null || ApplicationNavigation.IsInHierarchy (SuperView, view));
                OnEnter (view);
                ApplicationNavigation.Focused = this;

                //_hasFocus = true;
            }
            else
            {
                OnLeave (view);

                //_hasFocus = false;
            }

            SetNeedsDisplay ();
        }

        // Remove focus down the chain of subviews if focus is removed
        if (!newHasFocus && Focused is { })
        {
            View f = Focused;
            f.OnLeave (view);
            f.SetHasFocus (false, view, true);
            Focused = null;
        }
    }

    #region Tab/Focus Handling

#nullable enable

    private List<View>? _tabIndexes;

    // TODO: This should be a get-only property?
    // BUGBUG: This returns an AsReadOnly list, but isn't declared as such.
    /// <summary>Gets a list of the subviews that are a <see cref="TabStop"/>.</summary>
    /// <value>The tabIndexes.</value>
    public IList<View> TabIndexes => _tabIndexes?.AsReadOnly () ?? _empty;

    /// <summary>
    /// Gets TabIndexes that are scoped to the specified behavior and direction. If behavior is null, all TabIndexes are returned.
    /// </summary>
    /// <param name="behavior"></param>
    /// <param name="direction"></param>
    /// <returns></returns>GetScopedTabIndexes
    private View [] GetScopedTabIndexes (TabBehavior? behavior, NavigationDirection direction)
    {
        IEnumerable<View>? indicies;

        if (behavior.HasValue)
        {
            indicies = _tabIndexes?.Where (v => v.TabStop == behavior && v is { CanFocus: true, Visible: true, Enabled: true });
        }
        else
        {
            indicies = _tabIndexes?.Where (v => v is { CanFocus: true, Visible: true, Enabled: true });
        }

        if (direction == NavigationDirection.Backward)
        {
            indicies = indicies?.Reverse ();
        }

        return indicies?.ToArray () ?? Array.Empty<View> ();

    }

    private int? _tabIndex; // null indicates the view has not yet been added to TabIndexes
    private int? _oldTabIndex;

    /// <summary>
    ///     Indicates the order of the current <see cref="View"/> in <see cref="TabIndexes"/> list.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         If <see langword="null"/>, the view is not part of the tab order.
    ///     </para>
    ///     <para>
    ///         On set, if <see cref="SuperView"/> is <see langword="null"/> or has not TabStops, <see cref="TabIndex"/> will
    ///         be set to 0.
    ///     </para>
    ///     <para>
    ///         On set, if <see cref="SuperView"/> has only one TabStop, <see cref="TabIndex"/> will be set to 0.
    ///     </para>
    ///     <para>
    ///         See also <seealso cref="TabStop"/>.
    ///     </para>
    /// </remarks>
    public int? TabIndex
    {
        get => _tabIndex;

        // TOOD: This should be a get-only property. Introduce SetTabIndex (int value) (or similar).
        set
        {
            // Once a view is in the tab order, it should not be removed from the tab order; set TabStop to NoStop instead.
            Debug.Assert (value >= 0);
            Debug.Assert (value is { });

            if (SuperView?._tabIndexes is null || SuperView?._tabIndexes.Count == 1)
            {
                // BUGBUG: Property setters should set the property to the value passed in and not have side effects.
                _tabIndex = 0;

                return;
            }

            if (_tabIndex == value && TabIndexes.IndexOf (this) == value)
            {
                return;
            }

            _tabIndex = value > SuperView!.TabIndexes.Count - 1 ? SuperView._tabIndexes.Count - 1 :
                        value < 0 ? 0 : value;
            _tabIndex = GetGreatestTabIndexInSuperView ((int)_tabIndex);

            if (SuperView._tabIndexes.IndexOf (this) != _tabIndex)
            {
                // BUGBUG: we have to use _tabIndexes and not TabIndexes because TabIndexes returns is a read-only version of _tabIndexes
                SuperView._tabIndexes.Remove (this);
                SuperView._tabIndexes.Insert ((int)_tabIndex, this);
                UpdatePeerTabIndexes ();
            }
            return;

            // Updates the <see cref="TabIndex"/>s of the views in the <see cref="SuperView"/>'s to match their order in <see cref="TabIndexes"/>.
            void UpdatePeerTabIndexes ()
            {
                if (SuperView is null)
                {
                    return;
                }

                var i = 0;

                foreach (View superViewTabStop in SuperView._tabIndexes)
                {
                    if (superViewTabStop._tabIndex is null)
                    {
                        continue;
                    }

                    superViewTabStop._tabIndex = i;
                    i++;
                }
            }
        }
    }


    /// <summary>
    ///     Gets the greatest <see cref="TabIndex"/> of the <see cref="SuperView"/>'s <see cref="TabIndexes"/> that is less
    ///     than or equal to <paramref name="idx"/>.
    /// </summary>
    /// <param name="idx"></param>
    /// <returns>The minimum of <paramref name="idx"/> and the <see cref="SuperView"/>'s <see cref="TabIndexes"/>.</returns>
    private int GetGreatestTabIndexInSuperView (int idx)
    {
        if (SuperView is null)
        {
            return 0;
        }

        var i = 0;

        foreach (View superViewTabStop in SuperView._tabIndexes)
        {
            if (superViewTabStop._tabIndex is null || superViewTabStop == this)
            {
                continue;
            }

            i++;
        }

        return Math.Min (i, idx);
    }



    private TabBehavior? _tabStop;

    /// <summary>
    ///     Gets or sets the behavior of <see cref="AdvanceFocus"/> for keyboard navigation.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         If <see langword="null"/> the tab stop has not been set and setting <see cref="CanFocus"/> to true will set it
    ///         to
    ///         <see cref="TabBehavior.TabStop"/>.
    ///     </para>
    ///     <para>
    ///         TabStop is independent of <see cref="CanFocus"/>. If <see cref="CanFocus"/> is <see langword="false"/>, the
    ///         view will not gain
    ///         focus even if this property is set and vice-versa.
    ///     </para>
    ///     <para>
    ///         The default <see cref="TabBehavior.TabStop"/> keys are <c>Key.Tab</c> and <c>Key>Tab.WithShift</c>.
    ///     </para>
    ///     <para>
    ///         The default <see cref="TabBehavior.TabGroup"/> keys are <c>Key.Tab.WithCtrl</c> and <c>Key>Key.Tab.WithCtrl.WithShift</c>.
    ///     </para>
    /// </remarks>
    public TabBehavior? TabStop
    {
        get => _tabStop;
        set
        {
            if (_tabStop == value)
            {
                return;
            }

            Debug.Assert (value is { });

            if (_tabStop is null && TabIndex is null)
            {
                // This view has not yet been added to TabIndexes (TabStop has not been set previously).
                TabIndex = GetGreatestTabIndexInSuperView (SuperView is { } ? SuperView._tabIndexes.Count : 0);
            }

            _tabStop = value;
        }
    }

    #endregion Tab/Focus Handling
}
