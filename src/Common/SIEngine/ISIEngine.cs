﻿namespace SIEngine
{
    /// <summary>
    /// Implements SIGame engine allowing to play any SIGame package (<see cref="SIDocument" />).
    /// </summary>
    public interface ISIEngine
    {
        GameStage Stage { get; }

        int LeftQuestionsCount { get; }

        void MoveNext();

        void SelectQuestion(int theme, int question);

        void SelectTheme(int publicThemeIndex);

        int OnReady(out bool more);
    }
}
