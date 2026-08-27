using UnityEditor;

namespace Genix.Editor.State
{
    /// <summary>Tracks a transient editor message and its expiration time.</summary>
    public sealed class TimedMessage
    {
        /// <summary>Gets text.</summary>
        public string Text { get; private set; }
        /// <summary>Gets type.</summary>
        public MessageType Type { get; private set; }

        private double _visibleUntilTime;

        /// <summary>Indicates whether visible.</summary>
        public bool IsVisible => !string.IsNullOrEmpty(Text);

        /// <summary>Shows the message for the requested duration and severity.</summary>
        public void Show(string text, MessageType type = MessageType.Info, double durationSeconds = 4.0)
        {
            Text = text;
            Type = type;
            _visibleUntilTime = EditorApplication.timeSinceStartup + durationSeconds;
        }

        /// <summary>Clears the stored state.</summary>
        public void Clear()
        {
            Text = null;
            Type = MessageType.None;
            _visibleUntilTime = 0.0;
        }

        /// <summary>Updates the message lifetime and visibility state.</summary>
        public bool Update()
        {
            if (!IsVisible)
                return false;

            if (EditorApplication.timeSinceStartup < _visibleUntilTime)
                return true;

            Clear();
            return false;
        }
    }
}
