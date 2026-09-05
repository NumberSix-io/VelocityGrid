#pragma once

namespace winrt::VelocityGrid_Native::implementation
{
    struct VelocityGrid;

    // InteractionTracker owns this callback object. The back pointer is detached
    // during VelocityGrid shutdown, so the tracker never prolongs control life.
    struct InteractionTrackerOwner :
        winrt::implements<InteractionTrackerOwner,
            Microsoft::UI::Composition::Interactions::IInteractionTrackerOwner>
    {
        explicit InteractionTrackerOwner(VelocityGrid* owner) noexcept : m_owner(owner) {}

        void detach() noexcept { m_owner = nullptr; }

        void ValuesChanged(
            Microsoft::UI::Composition::Interactions::InteractionTracker const&,
            Microsoft::UI::Composition::Interactions::InteractionTrackerValuesChangedArgs const& args);
        void RequestIgnored(
            Microsoft::UI::Composition::Interactions::InteractionTracker const&,
            Microsoft::UI::Composition::Interactions::InteractionTrackerRequestIgnoredArgs const&) noexcept {}
        void InteractingStateEntered(
            Microsoft::UI::Composition::Interactions::InteractionTracker const&,
            Microsoft::UI::Composition::Interactions::InteractionTrackerInteractingStateEnteredArgs const&) noexcept {}
        void InertiaStateEntered(
            Microsoft::UI::Composition::Interactions::InteractionTracker const&,
            Microsoft::UI::Composition::Interactions::InteractionTrackerInertiaStateEnteredArgs const&) noexcept {}
        void IdleStateEntered(
            Microsoft::UI::Composition::Interactions::InteractionTracker const&,
            Microsoft::UI::Composition::Interactions::InteractionTrackerIdleStateEnteredArgs const&) noexcept;
        void CustomAnimationStateEntered(
            Microsoft::UI::Composition::Interactions::InteractionTracker const&,
            Microsoft::UI::Composition::Interactions::InteractionTrackerCustomAnimationStateEnteredArgs const&) noexcept {}

    private:
        VelocityGrid* m_owner;
    };
}
