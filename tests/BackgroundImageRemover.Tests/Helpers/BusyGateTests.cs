using BackgroundImageRemover.Helpers;
using CommunityToolkit.Mvvm.Input;

namespace BackgroundImageRemover.Tests.Helpers;

/// <summary>
/// Pins the reusable busy-gate contract: commands routed through <see cref="BusyGate.Gate"/>
/// are disabled while the gate is closed and re-evaluated automatically on every flip, while
/// <see cref="BusyGate.Track"/> re-evaluates without gating (the Cancel-command case).
/// </summary>
public class BusyGateTests
{
    [Fact]
    public void GatedCommand_IsDisabledWhileBusy_AndReenabledAfter()
    {
        var gate = new BusyGate();
        var executed = 0;
        var command = gate.Gate(new RelayCommand(() => executed++));

        Assert.True(command.CanExecute(null));

        gate.SetBusy(true);
        Assert.False(command.CanExecute(null)); // the gate alone disables it, no !IsBusy to write

        gate.SetBusy(false);
        Assert.True(command.CanExecute(null));

        command.Execute(null);
        Assert.Equal(1, executed);
    }

    [Fact]
    public void GatedCommand_CombinesTheInnerPredicateWithTheGate()
    {
        var gate = new BusyGate();
        var command = gate.Gate(new RelayCommand(() => { }, () => false));

        Assert.False(command.CanExecute(null)); // inner predicate already says no
        gate.SetBusy(true);
        Assert.False(command.CanExecute(null)); // and the gate keeps it off
    }

    [Fact]
    public void GatedCommand_RaisesCanExecuteChangedOnEveryBusyFlip()
    {
        var gate = new BusyGate();
        var command = gate.Gate(new RelayCommand(() => { }));
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        gate.SetBusy(true);
        gate.SetBusy(true); // no-op: same value must not re-raise
        gate.SetBusy(false);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void TrackedCommand_IsNotGated_ButIsReevaluatedOnBusyFlip()
    {
        var gate = new BusyGate();
        var busyAndSomething = false;
        var command = new RelayCommand(() => { }, () => busyAndSomething);
        gate.Track(command);

        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        // The tracked command answers its own predicate while busy (Cancel stays enabled)...
        busyAndSomething = true;
        gate.SetBusy(true);
        Assert.True(command.CanExecute(null));

        // ...but the flip still raised CanExecuteChanged so the UI re-asked.
        busyAndSomething = false;
        gate.SetBusy(false);
        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task AsyncGatedCommand_DelegatesExecutionAndExposesTheInnerTask()
    {
        var gate = new BusyGate();
        var executed = 0;
        var command = gate.Gate(new AsyncRelayCommand(async () =>
        {
            await Task.Yield();
            executed++;
        }));

        Assert.True(command.CanExecute(null));
        gate.SetBusy(true);
        Assert.False(command.CanExecute(null));
        gate.SetBusy(false);

        await command.ExecuteAsync(null);
        Assert.Equal(1, executed);
        Assert.NotNull(command.ExecutionTask);
    }

    [Fact]
    public void BusyChanged_RaisesWithTheNewValue()
    {
        var gate = new BusyGate();
        var observed = new List<bool>();
        gate.BusyChanged += value => observed.Add(value);

        gate.SetBusy(true);
        gate.SetBusy(false);

        Assert.Equal(new[] { true, false }, observed);
    }
}
