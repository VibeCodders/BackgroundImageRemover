using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Central gate for "a background run is in flight". A ViewModel owns one gate, exposes its
/// <c>IsBusy</c> flag, and routes every command that must not run while busy through
/// <see cref="Gate"/>. Gated commands answer <c>false</c> from CanExecute while the gate is
/// closed and are re-evaluated automatically on every busy flip — there is no per-command
/// <c>NotifyCanExecuteChangedFor</c> attribute or hand-written <c>!IsBusy &amp;&amp;</c> to
/// forget. Commands that must stay enabled while busy (Cancel-style commands) can be
/// registered with <see cref="Track"/> so they are still re-evaluated on flips.
/// </summary>
public sealed class BusyGate
{
    private readonly List<IRelayCommand> _gated = new();
    private readonly List<IRelayCommand> _tracked = new();
    private bool _isBusy;

    /// <summary>True while a background run is in flight; gated commands are disabled.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Raised every time the flag flips; the argument is the new value.</summary>
    public event Action<bool>? BusyChanged;

    /// <summary>Sets the busy flag and re-evaluates every registered command.</summary>
    public void SetBusy(bool value)
    {
        if (_isBusy == value)
        {
            return;
        }
        _isBusy = value;
        foreach (var command in _gated)
        {
            command.NotifyCanExecuteChanged();
        }
        foreach (var command in _tracked)
        {
            command.NotifyCanExecuteChanged();
        }
        BusyChanged?.Invoke(value);
    }

    /// <summary>
    /// Wraps a command so its CanExecute additionally requires the gate to be open. The
    /// wrapper is registered with the gate, so CanExecuteChanged is raised on every busy
    /// flip. The returned wrapper implements <see cref="IAsyncRelayCommand"/> and forwards
    /// to the wrapped command, so sync and async commands are both supported.
    /// </summary>
    public IAsyncRelayCommand Gate(IRelayCommand command)
    {
        var wrapped = new BusyAwareCommand(command, this);
        _gated.Add(wrapped);
        return wrapped;
    }

    /// <summary>
    /// Registers a command so it is re-evaluated on every busy flip without gating it —
    /// for Cancel-style commands whose CanExecute is "busy AND something".
    /// </summary>
    public void Track(IRelayCommand command) => _tracked.Add(command);
}

/// <summary>
/// Decorator that keeps a command disabled while the owning <see cref="BusyGate"/> is closed
/// (busy), delegating everything else to the wrapped command. Not created directly: use
/// <see cref="BusyGate.Gate"/>.
/// </summary>
public sealed class BusyAwareCommand : IAsyncRelayCommand
{
    private readonly IRelayCommand _inner;
    private readonly IAsyncRelayCommand? _asyncInner;
    private readonly BusyGate _gate;

    public BusyAwareCommand(IRelayCommand inner, BusyGate gate)
    {
        _inner = inner;
        _asyncInner = inner as IAsyncRelayCommand;
        _gate = gate;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => _inner.CanExecuteChanged += value;
        remove => _inner.CanExecuteChanged -= value;
    }

    // INotifyPropertyChanged: forward to the async inner when there is one (AsyncRelayCommand
    // announces IsRunning/ExecutionTask changes); a silent event otherwise, since a wrapped
    // sync command has no running state to announce.
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            if (_asyncInner is not null)
            {
                _asyncInner.PropertyChanged += value;
            }
        }
        remove
        {
            if (_asyncInner is not null)
            {
                _asyncInner.PropertyChanged -= value;
            }
        }
    }

    public bool CanExecute(object? parameter) => !_gate.IsBusy && _inner.CanExecute(parameter);

    public void Execute(object? parameter) => _inner.Execute(parameter);

    public Task ExecuteAsync(object? parameter)
        => _asyncInner is not null ? _asyncInner.ExecuteAsync(parameter) : Task.CompletedTask;

    public Task? ExecutionTask => _asyncInner?.ExecutionTask;

    public bool CanBeCanceled => _asyncInner?.CanBeCanceled ?? false;

    public bool IsCancellationRequested => _asyncInner?.IsCancellationRequested ?? false;

    public bool IsRunning => _asyncInner?.IsRunning ?? false;

    public void Cancel() => _asyncInner?.Cancel();

    public void NotifyCanExecuteChanged() => _inner.NotifyCanExecuteChanged();
}
