using System;
using UnityEditor;

namespace Genix.Editor.Genix.Editor.Common
{
    /// <summary>Groups a compound editor mutation into one user-visible Unity Undo operation.</summary>
    public static class UndoStep
    {
        /// <summary>Executes an action and collapses every nested mutation into one named Undo step.</summary>
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
