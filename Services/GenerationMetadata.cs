using System.Globalization;
using System.Text.Json;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Makes sense of what an image generator wrote into a PNG.
/// </summary>
/// <remarks>
/// Two formats are recognised because two are overwhelmingly common: ComfyUI
/// stores its whole node graph as JSON under <c>prompt</c>, and Automatic1111
/// stores a flat block under <c>parameters</c>. Anything else still shows up as
/// raw text — the point of this class is only to lift the handful of facts
/// someone actually wants out of a graph that can run to a hundred kilobytes.
///
/// Headline facts only, and from the first sampler. A graph with seven branches
/// has seven of everything, and listing them all would bury the answer to
/// "what made this picture" in the question's own working.
/// </remarks>
public static class GenerationMetadata
{
    /// <summary>
    /// What the generator recorded, or null when nothing recognisable is there.
    /// </summary>
    public static MetaGroup? Describe(IReadOnlyList<PngTextChunk> chunks)
    {
        var comfy = Find(chunks, "prompt");
        if (comfy is not null && Comfy(comfy) is { Count: > 0 } rows)
            return new MetaGroup("Generated with ComfyUI", rows);

        var auto = Find(chunks, "parameters");
        if (auto is not null && Automatic(auto) is { Count: > 0 } flat)
            return new MetaGroup("Generated with Automatic1111", flat);

        return null;
    }

    private static string? Find(IReadOnlyList<PngTextChunk> chunks, string key) =>
        chunks.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase))?.Text;

    // ------------------------------------------------------------------
    // ComfyUI
    // ------------------------------------------------------------------

    private static List<MetaRow>? Comfy(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

            // id → node, so the links between nodes can be followed. A link is
            // written as [id, slot], which is how a sampler names the thing
            // holding its prompt rather than holding the text itself.
            var nodes = new Dictionary<string, JsonElement>();
            foreach (var property in document.RootElement.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.Object)
                    nodes[property.Name] = property.Value;

            if (nodes.Count == 0) return null;

            var rows = new List<MetaRow>();

            foreach (var checkpoint in Values(nodes, "CheckpointLoader", "ckpt_name").Distinct())
                rows.Add(new MetaRow("Checkpoint", checkpoint));

            var loras = Loras(nodes);
            for (var i = 0; i < loras.Count && i < 6; i++)
                rows.Add(new MetaRow(loras.Count == 1 ? "LoRA" : $"LoRA {i + 1}", loras[i]));

            if (loras.Count > 6)
                rows.Add(new MetaRow("More LoRAs", $"and {loras.Count - 6} others"));

            Latent(nodes, rows);

            // Anything with "sampler" in its class, not only KSampler: a graph
            // built on SamplerCustom or SamplerCustomAdvanced has no node by
            // that name, and used to come out with no prompts and no settings
            // at all.
            var samplers = nodes.Values
                .Where(n => Class(n).Contains("Sampler", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (samplers.Count > 0)
            {
                Sampler(samplers[0], rows);

                if (samplers.Count > 1)
                    rows.Add(new MetaRow("Samplers", $"{samplers.Count} in the graph — the first is shown"));
            }

            // At the top, ahead of the settings: the prompt is what the window
            // is usually opened to read, and it was last on a list it had to be
            // scrolled past the end of.
            rows.InsertRange(0, Prompts(nodes, samplers.FirstOrDefault()));

            return rows;
        }
        catch
        {
            // Not the JSON this expects. The raw text is still shown, so
            // nothing is lost by giving up quietly here.
            return null;
        }
    }

    private static void Sampler(JsonElement sampler, List<MetaRow> rows)
    {
        var inputs = Inputs(sampler);
        if (inputs is null) return;

        Add(rows, "Sampler", Text(inputs.Value, "sampler_name"));
        Add(rows, "Scheduler", Text(inputs.Value, "scheduler"));
        Add(rows, "Steps", Number(inputs.Value, "steps"));
        Add(rows, "CFG", Number(inputs.Value, "cfg"));
        Add(rows, "Denoise", Number(inputs.Value, "denoise"));

        // KSampler calls it seed; KSamplerAdvanced calls it noise_seed.
        Add(rows, "Seed", Number(inputs.Value, "seed") ?? Number(inputs.Value, "noise_seed"));
    }

    private static void Latent(IReadOnlyDictionary<string, JsonElement> nodes, List<MetaRow> rows)
    {
        var latent = nodes.Values.FirstOrDefault(n =>
            Class(n).Contains("EmptyLatent", StringComparison.OrdinalIgnoreCase));

        var inputs = Inputs(latent);
        if (inputs is null) return;

        var width = Number(inputs.Value, "width");
        var height = Number(inputs.Value, "height");
        var batch = Number(inputs.Value, "batch_size");

        if (width is not null && height is not null)
            rows.Add(new MetaRow("Latent size",
                batch is null or "1" ? $"{width} × {height}" : $"{width} × {height}, batch {batch}"));
    }

    /// <summary>Each LoRA with the strength it was loaded at.</summary>
    private static List<string> Loras(IReadOnlyDictionary<string, JsonElement> nodes)
    {
        var found = new List<string>();

        foreach (var node in nodes.Values)
        {
            if (!Class(node).Contains("LoraLoader", StringComparison.OrdinalIgnoreCase)) continue;

            var inputs = Inputs(node);
            if (inputs is null) continue;

            var name = Text(inputs.Value, "lora_name");
            if (name is null) continue;

            // "None" is a slot left empty, which the graph still records.
            if (string.Equals(name, "None", StringComparison.OrdinalIgnoreCase)) continue;

            var strength = Number(inputs.Value, "strength_model");
            var entry = strength is null ? name : $"{name} at {strength}";

            if (!found.Contains(entry)) found.Add(entry);
        }

        return found;
    }

    /// <summary>
    /// The two prompts, positive first.
    /// </summary>
    /// <remarks>
    /// Following the sampler's own links is the reliable way round: which text
    /// node is the positive one is something only the thing consuming them
    /// knows. But it is not always possible — a custom sampler takes its
    /// conditioning through a guider node, a wildcard node may hold the words
    /// somewhere other than its text input — so when the walk comes back empty
    /// the text encoders are read directly instead. See
    /// <see cref="GuessPrompts"/>.
    /// </remarks>
    private static List<MetaRow> Prompts(IReadOnlyDictionary<string, JsonElement> nodes,
                                         JsonElement sampler)
    {
        string? positive = null, negative = null;

        if (Inputs(sampler) is { } inputs)
        {
            positive = Clean(FollowToText(nodes, inputs, "positive", depth: 0));
            negative = Clean(FollowToText(nodes, inputs, "negative", depth: 0));
        }

        if (positive is null || negative is null)
        {
            var (guessedPositive, guessedNegative) = GuessPrompts(nodes);
            positive ??= guessedPositive;
            negative ??= guessedNegative;
        }

        var rows = new List<MetaRow>();

        if (positive is not null) rows.Add(Prompt("Positive prompt", positive));
        if (negative is not null) rows.Add(Prompt("Negative prompt", negative));

        return rows;
    }

    /// <summary>
    /// The prompts read from the text encoders themselves, for a graph whose
    /// links could not be followed.
    /// </summary>
    /// <remarks>
    /// Which is which is then a guess, and it is made on the words rather than
    /// on the order the nodes happen to appear in: a negative prompt is a list
    /// of things to avoid, and that vocabulary is both small and consistent
    /// across everybody's. The one that reads least like such a list is taken
    /// as the positive one. With a single encoder in the graph there is nothing
    /// to weigh it against, so it is the positive prompt — a lone negative with
    /// no positive is not a picture anyone generated.
    /// </remarks>
    private static (string? Positive, string? Negative) GuessPrompts(
        IReadOnlyDictionary<string, JsonElement> nodes)
    {
        var texts = new List<string>();

        foreach (var node in nodes.Values)
        {
            if (!Class(node).Contains("TextEncode", StringComparison.OrdinalIgnoreCase)
                && !Class(node).Contains("CLIPText", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Inputs(node) is { } inputs && Clean(TextOf(inputs)) is { } text && !texts.Contains(text))
                texts.Add(text);
        }

        if (texts.Count == 0) return (null, null);
        if (texts.Count == 1) return (texts[0], null);

        var ordered = texts.OrderBy(NegativeScore).ToList();
        return (ordered[0], ordered[^1]);
    }

    /// <summary>
    /// How much a prompt reads like a list of things to avoid.
    /// </summary>
    private static int NegativeScore(string text)
    {
        string[] markers =
        {
            "worst quality", "low quality", "bad anatomy", "bad hands", "jpeg artifacts",
            "watermark", "signature", "username", "blurry", "lowres", "deformed",
            "disfigured", "extra limbs", "missing fingers", "cropped", "out of frame",
            "mutated", "ugly", "text, ", "error", "poorly drawn"
        };

        return markers.Count(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Walks a link until something holding text is reached.
    /// </summary>
    /// <remarks>
    /// Not always one hop: a prompt often passes through a combine or a style
    /// node on its way to the sampler. The depth limit stops a graph that links
    /// back on itself from walking forever.
    /// </remarks>
    private static string? FollowToText(IReadOnlyDictionary<string, JsonElement> nodes,
                                        JsonElement inputs, string slot, int depth)
    {
        if (depth > 6) return null;
        if (!inputs.TryGetProperty(slot, out var link)) return null;
        if (link.ValueKind != JsonValueKind.Array || link.GetArrayLength() == 0) return null;

        var id = link[0].ValueKind == JsonValueKind.String
            ? link[0].GetString()
            : link[0].ToString();

        if (id is null || !nodes.TryGetValue(id, out var node)) return null;

        var next = Inputs(node);
        if (next is null) return null;

        if (TextOf(next.Value) is { Length: > 0 } text) return text;

        // Not a text node: try whatever it is taking its own conditioning —
        // or its text — from. A prompt reaches the sampler through combine,
        // style, guidance and guider nodes, and through wildcard nodes that
        // take the words from a string node somewhere else again.
        foreach (var candidate in Carriers)
            if (FollowToText(nodes, next.Value, candidate, depth + 1) is { } found)
                return found;

        return null;
    }

    /// <summary>Inputs that one node uses to name another holding the text.</summary>
    private static readonly string[] Carriers =
    {
        "conditioning", "conditioning_1", "conditioning_2", "conditioning_to", "conditioning_from",
        "positive", "negative", "guider", "model", "text", "string", "value", "prompt"
    };

    /// <summary>
    /// The words a node holds, whatever it calls the box they are typed into.
    /// </summary>
    /// <remarks>
    /// <c>text</c> covers CLIPTextEncode and most of what is built on it. The
    /// rest are the names the common custom nodes use: SDXL's two encoders
    /// split the prompt into <c>text_g</c> and <c>text_l</c>, the wildcard
    /// nodes expose what they rolled as <c>populated_text</c>, and the string
    /// primitives call it <c>string</c> or <c>value</c>.
    /// </remarks>
    private static string? TextOf(JsonElement inputs)
    {
        foreach (var name in new[]
                 { "text", "populated_text", "wildcard_text", "prompt", "text_g", "text_l", "string", "value" })
            if (Text(inputs, name) is { Length: > 0 } found)
                return found;

        return null;
    }

    /// <summary>
    /// The prompt, and only the prompt.
    /// </summary>
    /// <remarks>
    /// What is taken out is everything that is not the words: LoRA and
    /// hypernetwork tags, which name a file and a strength and are already
    /// listed as rows of their own; the <c>embedding:</c> prefix, which is
    /// syntax rather than a word; and the line breaks a long prompt is typed
    /// across, which are not meaningful to read back.
    ///
    /// A value that turns out to be a fragment of the graph rather than a
    /// prompt is dropped entirely. Following a link into a node that holds a
    /// workflow is how JSON used to end up labelled as somebody's prompt.
    /// </remarks>
    private static string? Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var text = raw.Trim();

        // Code, not prose.
        if (text.StartsWith('{') || text.StartsWith('[')
            || text.Contains("class_type", StringComparison.Ordinal)
            || text.Contains("\"inputs\"", StringComparison.Ordinal))
            return null;

        // <lora:name:0.8>, <lyco:…>, <hypernet:…> — a tag, not a word.
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^<>]*>", " ");

        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\bembedding:\s*", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // One line, single spaces, and no comma left stranded by a tag that
        // used to sit between two of them.
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(,\s*){2,}", ", ");
        text = text.Trim().Trim(',').Trim();

        return text.Length > 0 ? text : null;
    }

    // ------------------------------------------------------------------
    // Automatic1111
    // ------------------------------------------------------------------

    /// <summary>
    /// The flat block A1111 writes: the prompt, then the negative prompt on its
    /// own line, then one line of comma-separated settings.
    /// </summary>
    private static List<MetaRow>? Automatic(string text)
    {
        const string negativeMarker = "Negative prompt:";

        var rows = new List<MetaRow>();

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var settingsAt = Array.FindLastIndex(lines, l => l.StartsWith("Steps:", StringComparison.Ordinal));
        var negativeAt = Array.FindIndex(lines, l => l.StartsWith(negativeMarker, StringComparison.Ordinal));

        var positiveEnd = negativeAt >= 0 ? negativeAt
            : settingsAt >= 0 ? settingsAt
            : lines.Length;

        if (Clean(string.Join("\n", lines.Take(positiveEnd))) is { } positive)
            rows.Add(Prompt("Positive prompt", positive));

        if (negativeAt >= 0)
        {
            var end = settingsAt > negativeAt ? settingsAt : lines.Length;
            var block = string.Join("\n", lines.Skip(negativeAt).Take(end - negativeAt))[negativeMarker.Length..];

            if (Clean(block) is { } negative)
                rows.Add(Prompt("Negative prompt", negative));
        }

        if (settingsAt >= 0)
            foreach (var setting in SplitSettings(lines[settingsAt]))
                rows.Add(setting);

        return rows;
    }

    /// <summary>
    /// "Steps: 20, Sampler: DPM++ 2M, CFG scale: 7" as rows of their own.
    /// </summary>
    /// <remarks>
    /// Split on commas that are followed by a word and a colon, so a sampler
    /// name or a hash containing a comma does not tear the line in half.
    /// </remarks>
    private static IEnumerable<MetaRow> SplitSettings(string line)
    {
        foreach (var part in System.Text.RegularExpressions.Regex.Split(line, @",\s+(?=[A-Za-z][\w\s]*:)"))
        {
            var colon = part.IndexOf(':');
            if (colon <= 0) continue;

            var name = part[..colon].Trim();
            var value = part[(colon + 1)..].Trim();

            if (name.Length > 0 && value.Length > 0) yield return new MetaRow(name, value);
        }
    }

    // ------------------------------------------------------------------
    // Shared
    // ------------------------------------------------------------------

    /// <summary>
    /// A prompt row: the words themselves.
    /// </summary>
    /// <remarks>
    /// Shown rather than counted. A long one used to read "428 characters —
    /// click to show", on the grounds that a prompt is long and not always what
    /// the window was opened for — but it is the thing people open this window
    /// to read, and a row that reports a length instead is a row that looks
    /// like the prompt is missing.
    /// </remarks>
    private static MetaRow Prompt(string name, string text) => new(name, text.Trim());

    private static string Class(JsonElement node) =>
        node.TryGetProperty("class_type", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static JsonElement? Inputs(JsonElement node) =>
        node.ValueKind == JsonValueKind.Object
        && node.TryGetProperty("inputs", out var inputs)
        && inputs.ValueKind == JsonValueKind.Object
            ? inputs
            : null;

    /// <summary>Every value of one input, across the nodes of one kind.</summary>
    private static IEnumerable<string> Values(IReadOnlyDictionary<string, JsonElement> nodes,
                                              string ofClass, string input)
    {
        foreach (var node in nodes.Values)
        {
            if (!Class(node).Contains(ofClass, StringComparison.OrdinalIgnoreCase)) continue;

            var inputs = Inputs(node);
            if (inputs is null) continue;

            if (Text(inputs.Value, input) is { Length: > 0 } value) yield return value;
        }
    }

    private static string? Text(JsonElement inputs, string name) =>
        inputs.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A numeric input as it was written, trailing zeros trimmed.</summary>
    private static string? Number(JsonElement inputs, string name)
    {
        if (!inputs.TryGetProperty(name, out var value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt64(out var whole)
                ? whole.ToString(CultureInfo.InvariantCulture)
                : value.GetDouble().ToString("0.####", CultureInfo.InvariantCulture),
            JsonValueKind.String => value.GetString(),
            _ => null
        };
    }

    private static void Add(List<MetaRow> rows, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) rows.Add(new MetaRow(name, value));
    }
}
