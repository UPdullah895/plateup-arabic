using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using TMPro;
using UnityEngine;

namespace PlateUpArabic
{
    /// <summary>
    /// Puts wrapped lines back into reading order.
    ///
    /// The text this mod ships is already in visual order, right-to-left, because TextMesh Pro
    /// has no bidi algorithm. That is correct for a single line, but when TMP runs out of width
    /// it breaks the line itself: its first line then holds the END of the sentence and the
    /// reader gets the paragraph backwards.
    ///
    /// TMP is the only thing that knows where those breaks fall, so rather than guess at widths
    /// during the build we let it wrap, then rewrite the text with the soft-wrapped lines of
    /// each paragraph in the opposite order and the breaks made explicit. Hard breaks written
    /// into the translation are already in reading order, so paragraphs keep their order and
    /// only the lines inside one are flipped.
    /// </summary>
    public static class Wrap
    {
        /// <summary>
        /// What a label last came in as and what we made of it. Some labels - the "hold any
        /// button to join" prompt on each bed, for one - are assigned afresh every frame, so this
        /// both stops our own rewrite being mistaken for new text and saves redoing the same work
        /// sixty times a second.
        /// </summary>
        private class Memo
        {
            public string Source;
            public string Rebuilt;
        }

        private static readonly ConditionalWeakTable<TMP_Text, Memo> Known =
            new ConditionalWeakTable<TMP_Text, Memo>();

        /// <summary>Tags that stay open until they are closed, and so have to be reopened on each
        /// line once the lines have been moved apart. Anything else (a sprite, a br) stands alone.</summary>
        private static readonly HashSet<string> Container = new HashSet<string>
        {
            "color", "b", "i", "u", "s", "size", "font", "align", "nobr", "mark", "sup", "sub",
            "cspace", "mspace", "line-height", "margin", "voffset", "style", "lowercase",
            "uppercase", "smallcaps", "allcaps", "gradient", "rotate", "width", "indent",
        };

        public static void Install()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Log.Line("wrap: watching for wrapped Arabic");
        }

        private static void OnTextChanged(UnityEngine.Object obj)
        {
            TMP_Text label = obj as TMP_Text;
            if (label == null)
            {
                return;
            }
            if (!label.enableWordWrapping)
            {
                return;
            }
            try
            {
                Fix(label);
            }
            catch (Exception e)
            {
                Log.Line("wrap failed: " + e);
            }
        }

        private static void Fix(TMP_Text label)
        {
            string source = label.text;
            if (string.IsNullOrEmpty(source) || !HasArabic(source))
            {
                return;
            }
            Memo memo;
            if (Known.TryGetValue(label, out memo))
            {
                if (source == memo.Rebuilt)
                {
                    return;     // our own rewrite coming back round
                }
                if (source == memo.Source)
                {
                    Pending[label] = memo.Rebuilt;  // the game has set it back; put it right again
                    return;
                }
            }
            TMP_TextInfo info = label.textInfo;
            if (info == null || info.lineCount < 2)
            {
                return;
            }
            string rebuilt = Reorder(source, info);
            if (rebuilt == null || rebuilt == source)
            {
                return;
            }
            Known.Remove(label);
            Known.Add(label, new Memo { Source = source, Rebuilt = rebuilt });
            // Assigning from inside TMP's own generation callback leaves the label dirty at a
            // point where the canvas has already decided what to rebuild, and the change is
            // dropped. Hand it to the end of the frame instead, after every Update that might
            // have set this text has run.
            Pending[label] = rebuilt;
        }

        /// <summary>
        /// A paragraph that had to be broken over several lines is ragged on whichever side the
        /// alignment leaves free, and for Arabic that has to be the left. Only paragraphs we have
        /// just reordered are touched: a label the game deliberately centres stays centred, and a
        /// single line that happens to sit left is left where the layout put it.
        ///
        /// In TextAlignmentOptions the low bits carry the horizontal part, 1 for left and 4 for
        /// right, and the high bits the vertical part, which stays as it is.
        /// </summary>
        private static void RightAlign(TMP_Text label)
        {
            int alignment = (int)label.alignment;
            if ((alignment & 1) != 0)
            {
                label.alignment = (TextAlignmentOptions)((alignment & ~1) | 4);
            }
        }

        private static readonly Dictionary<TMP_Text, string> Pending =
            new Dictionary<TMP_Text, string>();
        private static int Applied;

        /// <summary>Called by the driver from LateUpdate, so a label the game rewrites every
        /// frame is put right after that frame's assignment rather than before it.</summary>
        public static void Pump()
        {
            if (Pending.Count == 0)
            {
                return;
            }
            foreach (KeyValuePair<TMP_Text, string> entry in Pending)
            {
                if (entry.Key != null)
                {
                    entry.Key.text = entry.Value;
                    RightAlign(entry.Key);
                    Applied++;
                }
            }
            Pending.Clear();
            if (Applied == 1 || Applied % 500 == 0)
            {
                Log.Line("wrap: put " + Applied + " wrapped label(s) back into reading order");
            }
        }

        /// <summary>
        /// Rebuilds the text from TMP's own line breaks, reversing the lines within each
        /// paragraph. Returns null if nothing needed moving.
        /// </summary>
        private static string Reorder(string source, TMP_TextInfo info)
        {
            // Slice on the boundaries between lines rather than on each line's own first and
            // last character: a rich-text tag sits outside the characters it applies to, and
            // cutting to the characters alone would silently drop it.
            int[] bound = new int[info.lineCount + 1];
            for (int i = 0; i < info.lineCount; i++)
            {
                TMP_LineInfo line = info.lineInfo[i];
                if (line.characterCount == 0 || line.firstCharacterIndex >= info.characterCount)
                {
                    return null;
                }
                bound[i] = info.characterInfo[line.firstCharacterIndex].index;
                if (bound[i] < 0 || bound[i] > source.Length || (i > 0 && bound[i] < bound[i - 1]))
                {
                    return null;    // indices we do not understand; leave the text alone
                }
            }
            bound[0] = 0;
            bound[info.lineCount] = source.Length;

            List<string> paragraph = new List<string>();
            StringBuilder outer = new StringBuilder();
            bool moved = false;

            for (int i = 0; i < info.lineCount; i++)
            {
                string raw = source.Substring(bound[i], bound[i + 1] - bound[i]);
                paragraph.Add(raw.Trim());

                // A break the translation asked for lives in the text; one TMP invented does not.
                if (raw.IndexOf('\n') >= 0 || i == info.lineCount - 1)
                {
                    moved |= Flush(paragraph, outer, i < info.lineCount - 1);
                }
            }
            return moved ? outer.ToString() : null;
        }

        private static bool Flush(List<string> lines, StringBuilder outer, bool more)
        {
            if (lines.Count == 0)
            {
                return false;
            }
            bool moved = lines.Count > 1;
            StringBuilder para = new StringBuilder();
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                para.Append(lines[i]);
                if (i > 0)
                {
                    para.Append('\n');
                }
            }
            if (moved)
            {
                Balance(para);
            }
            outer.Append(para);
            if (more)
            {
                outer.Append('\n');
            }
            lines.Clear();
            return moved;
        }

        /// <summary>
        /// A tag opened on one line and closed on another was fine while the lines ran together;
        /// once they are reordered each line has to carry its own copy. This closes anything
        /// still open at the end of a line and reopens it at the start of the next.
        /// </summary>
        private static void Balance(StringBuilder text)
        {
            string[] lines = text.ToString().Split('\n');
            List<string> open = new List<string>();
            StringBuilder rebuilt = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                StringBuilder line = new StringBuilder();
                foreach (string tag in open)
                {
                    line.Append(tag);
                }
                line.Append(lines[i]);
                Track(lines[i], open);
                for (int t = open.Count - 1; t >= 0; t--)
                {
                    line.Append("</").Append(NameOf(open[t])).Append('>');
                }
                rebuilt.Append(line);
                if (i < lines.Length - 1)
                {
                    rebuilt.Append('\n');
                }
            }
            text.Length = 0;
            text.Append(rebuilt);
        }

        /// <summary>Updates the set of tags left open by a line.</summary>
        private static void Track(string line, List<string> open)
        {
            int at = 0;
            while ((at = line.IndexOf('<', at)) >= 0)
            {
                int shut = line.IndexOf('>', at);
                if (shut < 0)
                {
                    return;
                }
                string tag = line.Substring(at, shut - at + 1);
                string name = NameOf(tag);
                if (Container.Contains(name))
                {
                    if (tag.StartsWith("</"))
                    {
                        for (int i = open.Count - 1; i >= 0; i--)
                        {
                            if (NameOf(open[i]) == name)
                            {
                                open.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    else
                    {
                        open.Add(tag);
                    }
                }
                at = shut + 1;
            }
        }

        private static string NameOf(string tag)
        {
            int from = tag.StartsWith("</") ? 2 : 1;
            int to = from;
            while (to < tag.Length && tag[to] != '>' && tag[to] != '=' && tag[to] != ' ')
            {
                to++;
            }
            return tag.Substring(from, to - from).ToLowerInvariant();
        }

        /// <summary>True if the text picks up again after a newline at this point.</summary>
        private static bool IsHardBreak(string source, int end)
        {
            for (int i = end; i < source.Length; i++)
            {
                if (source[i] == '\n')
                {
                    return true;
                }
                if (!char.IsWhiteSpace(source[i]))
                {
                    return false;
                }
            }
            return false;
        }

        private static bool HasArabic(string text)
        {
            foreach (char c in text)
            {
                if (c >= 0x0600 && c <= 0x06FF || c >= 0xFB50 && c <= 0xFEFF)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
