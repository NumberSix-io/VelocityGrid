#include "pch.h"
#include "InteractionTrackerOwner.h"
#include "../VelocityGrid.h"

namespace winrt::VelocityGrid_Native::implementation
{
    void InteractionTrackerOwner::ValuesChanged(
        Microsoft::UI::Composition::Interactions::InteractionTracker const&,
        Microsoft::UI::Composition::Interactions::InteractionTrackerValuesChangedArgs const& args)
    {
        if (m_owner) m_owner->on_interaction_tracker_values_changed(args);
    }

    void InteractionTrackerOwner::IdleStateEntered(
        Microsoft::UI::Composition::Interactions::InteractionTracker const&,
        Microsoft::UI::Composition::Interactions::InteractionTrackerIdleStateEnteredArgs const&) noexcept
    {
        if (!m_owner) return;
        try { m_owner->on_interaction_tracker_idle(); }
        catch (...) {}
    }
}
