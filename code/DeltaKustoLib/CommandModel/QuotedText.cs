﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeltaKustoLib.CommandModel
{
    public class QuotedText
    {
        public QuotedText(string text)
        {
            Text = text;
        }

        public static QuotedText? FromText(string? text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? null
                : new QuotedText(text);
        }

        public string Text { get; }

        public string ToScript()
        {
            var escape = Text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            return $"\"{escape}\"";
        }

        #region object methods
        public override string ToString()
        {
            return ToScript();
        }

        public override bool Equals(object? obj)
        {
            var other = obj as QuotedText;

            return other != null
                && other.Text == Text;
        }

        public override int GetHashCode()
        {
            return Text.GetHashCode();
        }
        #endregion
    }
}