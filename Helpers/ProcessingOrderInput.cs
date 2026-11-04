using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Reads a typed processing order — "3, 1, 2, 6, 5, 4" — back into the cuts it
/// names.
/// </summary>
/// <remarks>
/// Separated from the dialog because it is the part worth testing: the dialog
/// calls it on every keystroke to say what the box means, and again on Save to
/// act on it, and those two answers have to be the same one.
///
/// Every cut exactly once. A partial order would have to say where the cuts it
/// left out went, and any answer to that is a rule the user has to learn before
/// they can predict what their own order does. "List them all" needs no rule.
/// </remarks>
public static class ProcessingOrderInput
{
    /// <summary>
    /// Turns typed text into a sequence of cuts, or says what is wrong with it.
    /// </summary>
    /// <param name="text">Numbers separated by commas, spaces, or both.</param>
    /// <param name="cuts">The list the numbers refer to, in row order.</param>
    public static bool TryParse(
        string? text,
        IReadOnlyList<Bookmark> cuts,
        out List<Bookmark> order,
        out string problem)
    {
        // Built aside and handed over only on success, so a caller that reads
        // the list without checking the bool gets nothing rather than the
        // first few clips of an order that was refused.
        order = new List<Bookmark>();
        problem = string.Empty;

        var reading = new List<Bookmark>();

        var pieces = (text ?? string.Empty)
            .Split(new[] { ',', ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);

        if (pieces.Length == 0)
        {
            problem = "Type the clip numbers in the order you want them run.";
            return false;
        }

        var seen = new HashSet<int>();

        foreach (var piece in pieces)
        {
            if (!int.TryParse(piece, out var number))
            {
                problem = $"\"{piece}\" is not a clip number.";
                return false;
            }

            if (number < 1 || number > cuts.Count)
            {
                problem = cuts.Count == 1
                    ? $"There is only clip 1 — {number} does not exist."
                    : $"Clip {number} does not exist. The clips are 1 to {cuts.Count}.";
                return false;
            }

            if (!seen.Add(number))
            {
                problem = $"Clip {number} is listed more than once.";
                return false;
            }

            reading.Add(cuts[number - 1]);
        }

        // Named last, because a list that is simply unfinished is the common
        // way to arrive here and the missing numbers are the useful half of
        // the answer. Saying "6 numbers expected, 4 given" would leave the
        // user to work out which four they had already typed.
        if (seen.Count != cuts.Count)
        {
            var missing = Enumerable.Range(1, cuts.Count).Where(n => !seen.Contains(n)).ToList();
            problem = missing.Count == 1
                ? $"Clip {missing[0]} is missing — every clip has to be listed."
                : $"Clips {string.Join(", ", missing)} are missing — every clip has to be listed.";
            return false;
        }

        order = reading;
        return true;
    }
}
