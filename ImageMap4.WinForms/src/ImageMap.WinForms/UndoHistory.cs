using System;
using System.Collections.Generic;

namespace ImageMap4
{
    public class UndoHistory
    {
        private readonly Stack<Undoable> _undos = new();
        private readonly Stack<Undoable> _redos = new();

        public void Perform(Action action, Action undo)
        {
            action();
            _undos.Push(new Undoable(action, undo) { Context = null });
            _redos.Clear();
        }

        public void PerformContext<TActionContext, TUndoContext>(
            Func<TUndoContext?, TActionContext?> action,
            Func<TActionContext?, TUndoContext?> undo)
        {
            var result = action(default);
            _undos.Push(new Undoable(x => action((TUndoContext)x!), x => undo((TActionContext)x!)) { Context = result });
            _redos.Clear();
        }

        public void Undo()
        {
            if (!CanUndo)
                return;
            var action = _undos.Pop();
            action.Context = action.Undo(action.Context);
            _redos.Push(action);
        }

        public void Redo()
        {
            if (!CanRedo)
                return;
            var action = _redos.Pop();
            action.Context = action.Action(action.Context);
            _undos.Push(action);
        }

        public void Clear()
        {
            _undos.Clear();
            _redos.Clear();
        }

        public bool CanUndo => _undos.Count > 0;
        public bool CanRedo => _redos.Count > 0;

        private sealed class Undoable
        {
            public readonly Func<object?, object?> Action;
            public readonly Func<object?, object?> Undo;
            public object? Context;

            public Undoable(Func<object?, object?> action, Func<object?, object?> undo)
            {
                Action = action;
                Undo = undo;
            }

            public Undoable(Action action, Action undo)
            {
                Action = _ => { action(); return null; };
                Undo = _ => { undo(); return null; };
            }
        }
    }
}
