using System;
using UnityEditor;

namespace Genix.Editor.Genix.Editor.Common
{
    public static class UndoStep
    {
        public static void ExecuteAsSingleStep(string undoName, Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            Undo.IncrementCurrentGroup();

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);

            try
            {
                action.Invoke();
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }
    }
}