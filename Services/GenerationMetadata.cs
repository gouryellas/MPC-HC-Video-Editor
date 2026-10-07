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

            var samplers = nodes.Values
                .Where(n => Class(n).Contains("KSampler", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (samplers.Count > 0)
            {
                Sampler(samplers[0], rows);

                if (samplers.Count > 1)
                    rows.Add(new MetaRow("Samplers", $"{samplers.Count} in the graph — the first is shown"));

                Prompts(nodes, samplers[0], rows);
            }

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
    /// The two prompts, found by following the sampler's own links rather than
    /// by collecting every text node in the graph — which way round they go is
    /// something only the sampler knows.
    /// </summary>
    private static void Prompts(IReadOnlyDictionary<string, JsonElement> nodes,
                                JsonElement sampler, List<MetaRow> rows)
    {
        var inputs = Inputs(sampler);
        if (inputs is null) return;

        foreach (var (slot, label) in new[] { ("positive", "Positive prompt"), ("negative", "Negative prompt") })
        {
            var text = FollowToText(nodes, inputs.Value, slot, depth: 0);
            if (text is null) continue;

            rows.Add(Prompt(label, text));
        }
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

        if (Text(next.Value, "text") is { Length: > 0 } text) return text;

        // Not a text node: try whatever it is taking its own conditioning from.
        foreach (var candidate in new[] { "conditioning_1", "conditioning", "conditioning_to", "text" })
            if (FollowToText(nodes, next.Value, candidate, depth + 1) is { } found)
                return found;

        return null;
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

        var positive = string.Join("\n", lines.Take(positiveEnd)).Trim();
        if (positive.Length > 0) rows.Add(Prompt("Positive prompt", positive));

        if (negativeAt >= 0)
        {
            var end = settingsAt > negativeAt ? settingsAt : lines.Length;
            var negative = string.Join("\n", lines.Skip(negativeAt).Take(end - negativeAt))
                                 [negativeMarker.Length..].Trim();

            if (negative.Length > 0) rows.Add(Prompt("Negative prompt", negative));
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
    /// A prompt row: its length, with the words themselves behind a click.
    /// </summary>
    /// <remarks>
    /// Collapsed because a prompt is long, is the one part of this a person
    /// might not want on screen, and is rarely what the window was opened to
    /// find out. Short ones are shown outright — hiding six words behind a
    /// disclosure triangle is ceremony for its own sake.
    /// </remarks>
    private static MetaRow Prompt(string name, string text)
    {
        var tidy = text.Trim();

        return tidy.Length <= 80
            ? new MetaRow(name, tidy)
            : new MetaRow(name, $"{tidy.Length} characters — click to show", tidy);
    }

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
