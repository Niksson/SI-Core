﻿using Notions;
using SIQuester.ViewModel.PlatformSpecific;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace SIQuester.ViewModel
{
    public sealed class AnswersViewModel: ItemsViewModel<string>
    {
        public QuestionViewModel Owner { get; private set; }

        public SimpleCommand AnswerSpecial1 { get; private set; }
        public SimpleCommand AnswerSpecial2 { get; private set; }
        public SimpleCommand AnswerSpecial3 { get; private set; }

        public ICommand ToNewAnswer { get; private set; }
        public ICommand ToNewSource { get; private set; }
        public ICommand ToNewComment { get; private set; }

        public ICommand SelectAtomObject { get; private set; }

        public override QDocument OwnerDocument => Owner.OwnerTheme.OwnerRound.OwnerPackage.Document;

        public bool IsRight { get; private set; }

        public AnswersViewModel(QuestionViewModel owner, IEnumerable<string> collection, bool isRight)
            : base(collection)
        {
            Owner = owner;
            IsRight = isRight;

            AnswerSpecial1 = new SimpleCommand(AnswerSpecial1_Executed);
            AnswerSpecial2 = new SimpleCommand(AnswerSpecial2_Executed);
            AnswerSpecial3 = new SimpleCommand(AnswerSpecial3_Executed);

            ToNewAnswer = new SimpleCommand(ToNewAnswer_Executed);
            ToNewSource = new SimpleCommand(ToNewSource_Executed);
            ToNewComment = new SimpleCommand(ToNewComment_Executed);

            SelectAtomObject = new SimpleCommand(SelectAtomObject_Executed);

            UpdateCommands();
            UpdateAnswersCommands();
        }

        protected override void OnCurrentItemChanged(string oldValue, string newValue)
        {
            base.OnCurrentItemChanged(oldValue, newValue);
            UpdateAnswersCommands();
        }

        public void UpdateAnswersCommands()
        {
            var text = CurrentItem;
            AnswerSpecial1.CanBeExecuted = !string.IsNullOrEmpty(text) && text.Contains(" ");
            AnswerSpecial2.CanBeExecuted = text != null && text.Contains("(") && text.Contains(")");
            AnswerSpecial3.CanBeExecuted = text != null && text.Contains(" и ");
        }

        public override string ToString() => string.Join(", ", this);

        private void AnswerSpecial1_Executed(object arg)
        {
            var text = CurrentItem;
            var words = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var s = words[words.Length - 1];
            Add(s.GrowFirstLetter());
        }

        private void AnswerSpecial2_Executed(object arg)
        {
            var document = Owner.OwnerTheme.OwnerRound.OwnerPackage.Document;

            document.BeginChange();
            try
            {
                var index = CurrentPosition;
                var text = this[index];
                var s = text.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                if (s.Length > 1)
                {
                    var comments = Owner.Info.Comments;
                    if (comments.Text.Length > 0)
                    {
                        comments.Text += Environment.NewLine;
                    }

                    comments.Text += s[1].GrowFirstLetter();
                    var str = new StringBuilder(s[0].Trim());
                    var i = 2;
                    while (i < s.Length)
                    {
                        str.Append(' ');
                        str.Append(s[i++].Trim());
                    }

                    this[index] = str.ToString();
                }

                document.CommitChange();
            }
            catch (Exception exc)
            {
                document.RollbackChange();
                document.OnError(exc);
            }
        }

        private void AnswerSpecial3_Executed(object arg)
        {
            var text = CurrentItem;
            int i = text.IndexOf(" и ");
            if (i > -1)
            {
                Add(string.Format("{0} и {1}", text.Substring(i + 3).GrowFirstLetter(), text.Substring(0, i)));
            }
        }

        private string ProcessSelection()
        {
            var selection = PlatformManager.Instance.GetCurrentItemSelectionArea();
            if (selection == null || selection.Item3 == 0)
                return null;

            if (selection.Item1 < 0 || selection.Item1 >= Count)
                throw new Exception("ProcessSelection error Item1: " + selection.Item1 + " " + Count);

            var item = this[selection.Item1];

            if (selection.Item2 < 0 || selection.Item2 > item.Length)
                throw new Exception("ProcessSelection error Item2: " + selection.Item2 + " " + item.Length);

            if (selection.Item3 < 0 || selection.Item2 + selection.Item3 > item.Length)
                throw new Exception("ProcessSelection error Item3: " + selection.Item3 + " " + item.Length);

            var text = item.Substring(selection.Item2, selection.Item3).Trim().GrowFirstLetter();

            var start = selection.Item2;
            var end = selection.Item2 + selection.Item3;

            var emptyLeft = new char[] { ' ', '/', '(', ',', '\\', '—' };
            while (start > 0 && emptyLeft.Contains(item[start - 1]))
                start--;

            var emptyRight = new char[] { ' ', ')' };
            while (end < item.Length && emptyRight.Contains(item[end]))
                end++;

            this[selection.Item1] =
                (start > 0 ? item.Substring(0, start) : "")
                + (end < item.Length ? item.Substring(end) : "");

            return text;
        }

        private void ToNewAnswer_Executed(object arg)
        {
            var text = ProcessSelection();
            if (text == null)
            {
                return;
            }

            Add(text);
        }

        private void ToNewSource_Executed(object arg)
        {
            try
            {
                var text = ProcessSelection();
                if (text == null)
                {
                    return;
                }

                var sources = Owner.Info.Sources;
                sources.Add(text);
            }
            catch (Exception exc)
            {
                PlatformManager.Instance.ShowExclamationMessage(exc.Message);
            }
        }

        private void ToNewComment_Executed(object arg)
        {
            var text = ProcessSelection();
            if (text == null)
            {
                return;
            }

            var comments = Owner.Info.Comments;
            if (comments.Text.Length > 0)
            {
                comments.Text += Environment.NewLine;
            }

            comments.Text += text;
        }

        protected override bool CanRemove() => Count > 1 || Owner == null || Owner.Wrong == this;

        private void SelectAtomObject_Executed(object arg)
        {
            Owner.Scenario.SelectAtomObject_AsAnswer(arg);
            OwnerDocument.ActiveItem = this;
        }
    }
}
