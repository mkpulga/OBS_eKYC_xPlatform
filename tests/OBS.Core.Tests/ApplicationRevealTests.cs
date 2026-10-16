using OBS.Core;

static class ApplicationRevealTests
{
    public static void Register(Action<string, Func<Task>> add, Action<bool, string> check)
    {
        add("Applicant groups start hidden with independent per-instance state", () =>
        {
            var state = new ApplicationRevealState();
            check(Enum.GetValues<ApplicationGroup>().Length == 5, "Expected five groups including Address");
            foreach (var group in Enum.GetValues<ApplicationGroup>()) check(!state.IsVisible(group), "Group starts revealed");
            state.Toggle(ApplicationGroup.Profile);
            check(!new ApplicationRevealState().IsVisible(ApplicationGroup.Profile), "Reveal persisted between instances");
            return Task.CompletedTask;
        });
        add("Applicant toggles affect only their group and toggle twice to hide", () =>
        {
            foreach (var selected in Enum.GetValues<ApplicationGroup>())
            {
                var state = new ApplicationRevealState();
                state.Toggle(selected);
                foreach (var group in Enum.GetValues<ApplicationGroup>()) check(state.IsVisible(group) == (group == selected), "Toggle changed another group");
                state.Toggle(selected);
                check(!state.IsVisible(selected), "Second toggle did not hide");
            }
            return Task.CompletedTask;
        });
        add("Applicant reset hides every revealed group", () =>
        {
            var state = new ApplicationRevealState();
            foreach (var group in Enum.GetValues<ApplicationGroup>()) state.Toggle(group);
            state.Reset(); state.Reset();
            foreach (var group in Enum.GetValues<ApplicationGroup>()) check(!state.IsVisible(group), "Reset left a group visible");
            return Task.CompletedTask;
        });
        add("Applicant hidden display uses the same fixed mask for real and missing values", () =>
        {
            var state = new ApplicationRevealState();
            foreach (var group in Enum.GetValues<ApplicationGroup>())
            {
                foreach (var value in new[] { null, "", " ", "Alex Santos Cruz", "a", new string('x', 200), "Not provided", "Year not provided" })
                {
                    var display = state.Display(group, value);
                    check(display == ApplicationRevealState.Mask && display.Length == 8, "Hidden display leaked value or length");
                    if (!string.IsNullOrWhiteSpace(value)) check(!display.Contains(value), "Hidden display contains real value");
                }
                state.Toggle(group);
                check(state.Display(group, " Alex ") == "Alex" && state.Display(group, "") == "Not provided", "Visible display does not use presentation rules");
                state.Toggle(group);
                check(state.Display(group, "Alex") == ApplicationRevealState.Mask, "Re-hidden display leaked value");
            }
            return Task.CompletedTask;
        });
        add("Account popup keeps address hidden independently of profile", () =>
        {
            var state = new ApplicationRevealState();
            var address = new ApplicationField("Permanent address", "123 Sample Street");
            state.Toggle(ApplicationGroup.Profile);
            check(state.DisplayField(ApplicationGroup.Profile, address) == ApplicationRevealState.Mask, "Profile revealed address");
            check(state.DisplayField(ApplicationGroup.Profile, new("Email", "a@example.invalid")) == "a@example.invalid", "Profile field stayed hidden");
            state.Toggle(ApplicationGroup.Address);
            check(state.DisplayField(ApplicationGroup.Profile, address) == "123 Sample Street", "Address toggle did not reveal address");
            state.Toggle(ApplicationGroup.Profile);
            check(state.IsVisible(ApplicationGroup.Address), "Profile toggle changed address");
            state.Reset();
            check(state.DisplayField(ApplicationGroup.Profile, address) == ApplicationRevealState.Mask, "Reset leaked address");
            return Task.CompletedTask;
        });
        add("Applicant details are allowed only for visible groups", () =>
        {
            var state = new ApplicationRevealState();
            foreach (var selected in Enum.GetValues<ApplicationGroup>())
            {
                check(!state.DetailsAllowed(selected), "Hidden details allowed");
                state.Toggle(selected);
                foreach (var group in Enum.GetValues<ApplicationGroup>()) check(state.DetailsAllowed(group) == (group == selected), "Details gating leaked across groups");
                state.Toggle(selected);
                check(!state.DetailsAllowed(selected), "Re-hidden details allowed");
            }
            state.Toggle(ApplicationGroup.Profile); state.Reset();
            check(!state.DetailsAllowed(ApplicationGroup.Profile), "Reset left account details allowed");
            return Task.CompletedTask;
        });
    }
}
