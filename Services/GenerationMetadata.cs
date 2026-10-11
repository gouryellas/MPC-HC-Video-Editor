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
        if (comfy is not null && Comfy(comfy, Cached(Find(chunks, "workflow"))) is { Count: > 0 } rows)
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

    /// <summary>
    /// The text each node's own box last held, taken from the saved workflow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A prompt is not always written down. A workflow can build one while it
    /// runs — a switch choosing between branches, a concatenate joining a LoRA
    /// name to a phrase, a conditional picking one of three endings — and the
    /// <c>prompt</c> chunk records what fed each node, not what came out. Walk
    /// that graph and the text encoder has no words in it at all, because its
    /// text arrived down a wire.
    /// </para>
    /// <para>
    /// The <c>workflow</c> chunk is the other half. It is the editor's own copy
    /// of the graph, and it keeps each node's widget values — including the box
    /// that was converted into an input, which holds what that node was last
    /// given. For an assembled prompt that is the assembled text, which exists
    /// nowhere else in the file.
    /// </para>
    /// <para>
    /// A fallback, never a first choice: it is the last value the box held
    /// rather than a record of this run, so anything the graph states outright
    /// is believed ahead of it.
    /// </para>
    /// </remarks>
    private static Dictionary<string, string> Cached(string? workflow)
    {
        var cached = new Dictionary<string, string>();
        if (workflow is null) return cached;

        try
        {
            using var document = JsonDocument.Parse(workflow);
            if (!document.RootElement.TryGetProperty("nodes", out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
                return cached;

            foreach (var node in nodes.EnumerateArray())
            {
                if (!node.TryGetProperty("id", out var id)) continue;
                if (!node.TryGetProperty("widgets_values", out var values)
                    || values.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var value in values.EnumerateArray())
                {
                    if (value.ValueKind != JsonValueKind.String) continue;

                    var text = value.GetString();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var key = id.ValueKind == JsonValueKind.String ? id.GetString() : id.ToString();
                    if (key is not null) cached.TryAdd(key, text);
                    break;
                }
            }
        }
        catch
        {
            // Not the JSON this expects; the graph is still read on its own.
        }

        return cached;
    }

    private static List<MetaRow>? Comfy(string json, Dictionary<string, string> cached)
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

            if (samplers.Count > 0) Sampler(samplers[0], rows);

            // Last, under the seed: the settings are a short column of short
            // values that read as a block, and a prompt is a paragraph. Putting
            // the paragraph first pushed the block off the bottom of the window;
            // putting it between them broke the block in half.
            rows.AddRange(Prompts(nodes, samplers.FirstOrDefault(), cached));

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
                                         JsonElement sampler,
                                         IReadOnlyDictionary<string, string> cached)
    {
        string? positive = null, negative = null;

        if (Conditioning(nodes, Inputs(sampler), depth: 0) is { } inputs)
        {
            positive = Clean(FollowToText(nodes, cached, inputs, "positive", "positive", depth: 0));
            negative = Clean(FollowToText(nodes, cached, inputs, "negative", "negative", depth: 0));
        }

        // One chain reaching the other's text is a real shape, not a bug in the
        // walk: a negative built by zeroing out the positive conditioning holds
        // no words of its own, and the only text at the end of that chain is the
        // positive prompt. Showing it twice would be inventing a negative prompt
        // the image does not have.
        if (positive is not null && positive == negative) negative = null;

        if (positive is null || negative is null)
        {
            // Whatever is already known is kept out of the guess, so a prompt
            // cannot be offered as both answers.
            var (guessedPositive, guessedNegative) = GuessPrompts(nodes, cached, positive ?? negative);

            positive ??= guessedPositive;
            negative ??= guessedNegative;
        }

        if (positive is not null && positive == negative) negative = null;

        var rows = new List<MetaRow>();

        if (positive is not null) rows.Add(Prompt("Positive prompt", positive));
        if (negative is not null) rows.Add(Prompt("Negative prompt", negative));

        return rows;
    }

    /// <summary>
    /// The inputs that actually name the two prompts, which are not always the
    /// sampler's own.
    /// </summary>
    /// <remarks>
    /// SamplerCustomAdvanced takes neither a positive nor a negative: it takes
    /// a guider, and the guider holds both. Starting the walk at the sampler
    /// found nothing to walk from, so a graph built this way produced no
    /// prompts at all. A couple of hops is enough — a guider named by a guider
    /// is not a thing anyone builds, but the depth limit means a graph that
    /// links back on itself cannot spin here.
    /// </remarks>
    private static JsonElement? Conditioning(IReadOnlyDictionary<string, JsonElement> nodes,
                                             JsonElement? inputs, int depth)
    {
        if (inputs is null || depth > 3) return null;

        if (inputs.Value.TryGetProperty("positive", out _) ||
            inputs.Value.TryGetProperty("negative", out _))
            return inputs;

        foreach (var hop in new[] { "guider", "sampler", "model" })
        {
            if (!inputs.Value.TryGetProperty(hop, out var link)) continue;
            if (link.ValueKind != JsonValueKind.Array || link.GetArrayLength() == 0) continue;

            var id = link[0].ValueKind == JsonValueKind.String ? link[0].GetString() : link[0].ToString();
            if (id is null || !nodes.TryGetValue(id, out var node)) continue;

            if (Conditioning(nodes, Inputs(node), depth + 1) is { } found) return found;
        }

        return null;
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
    ///
    /// Two prompts that both score zero used to come back in whatever order the
    /// graph happened to list its nodes in, which is how a negative prompt came
    /// to be shown as the positive one. Ties now leave the positive unanswered
    /// rather than guessing at it: a blank row is a smaller lie than a confident
    /// wrong one, and the words are still in the raw text below.
    /// </remarks>
    private static (string? Positive, string? Negative) GuessPrompts(
        IReadOnlyDictionary<string, JsonElement> nodes,
        IReadOnlyDictionary<string, string> cached, string? known)
    {
        var texts = new List<string>();

        foreach (var (id, node) in nodes)
        {
            if (!Class(node).Contains("TextEncode", StringComparison.OrdinalIgnoreCase)
                && !Class(node).Contains("CLIPText", StringComparison.OrdinalIgnoreCase))
                continue;

            var words = Inputs(node) is { } inputs ? TextOf(inputs) : null;
            words ??= Assembled(nodes, id, depth: 0);
            words ??= cached.GetValueOrDefault(id);

            if (Clean(words) is { } text && text != known && !texts.Contains(text))
                texts.Add(text);
        }

        if (texts.Count == 0) return (null, null);
        if (texts.Count == 1) return (texts[0], null);

        var ordered = texts.OrderBy(NegativeScore).ToList();

        var best = NegativeScore(ordered[0]);
        var worst = NegativeScore(ordered[^1]);

        // Nothing to tell them apart by. Answering anyway is a coin toss, and
        // the wrong side of it puts the list of things to avoid under the
        // heading of what was asked for.
        return best == worst ? (null, null) : (ordered[0], ordered[^1]);
    }

    // ------------------------------------------------------------------
    // Prompts the workflow builds as it runs
    // ------------------------------------------------------------------

    /// <summary>
    /// Works out what a text-building node produces, by doing what it does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A prompt is not always typed into a box. It can be assembled: a name
    /// pulled out of a LoRA's filename by a regular expression, joined to a
    /// phrase, finished with one of several endings chosen by what the name
    /// turned out to be. None of that is written down anywhere — the file
    /// records the recipe, and the only way to the words is to follow it.
    /// </para>
    /// <para>
    /// A handful of node kinds covers the ones that actually appear in prompt
    /// chains: concatenate, replace, switch, conditional branch, and the string
    /// primitives at the ends. Anything unrecognised returns null rather than a
    /// guess, which puts the walk back on its ordinary path.
    /// </para>
    /// </remarks>
    private static string? Assembled(IReadOnlyDictionary<string, JsonElement> nodes,
                                     string id, int depth)
    {
        // Deep enough for a real chain and shallow enough to stop a graph
        // that links back on itself. Ten was not: a prompt five nodes long
        // reaches twice that, counting a step for every input in between, and
        // the walk ran out exactly at the leaf holding the words.
        if (depth > 32 || !nodes.TryGetValue(id, out var node)) return null;
        if (Inputs(node) is not { } inputs) return null;

        var kind = Class(node);

        if (kind.Contains("Concatenate", StringComparison.OrdinalIgnoreCase))
        {
            var delimiter = Text(inputs, "delimiter") ?? " ";

            // Only when the node says so. Trimming regardless turned a piece
            // that was deliberately " from " into "from", and a concatenate
            // joining on an empty delimiter then ran its words together.
            var tidy = string.Equals(Text(inputs, "clean_whitespace"), "true",
                                     StringComparison.OrdinalIgnoreCase);

            var parts = inputs.EnumerateObject()
                .Where(p => p.Name.StartsWith("text_", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => Value(nodes, p.Value, depth + 1))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => tidy ? v!.Trim() : v!);

            var joined = string.Join(delimiter, parts);
            return joined.Length > 0 ? joined : null;
        }

        if (kind.Contains("TextReplace", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("RegexReplace", StringComparison.OrdinalIgnoreCase))
            return Replaced(nodes, inputs, depth);

        if (kind.Contains("ConditionalBranch", StringComparison.OrdinalIgnoreCase))
        {
            var chosen = Truth(nodes, inputs, depth) ? "tt_value" : "ff_value";
            return inputs.TryGetProperty(chosen, out var branch) ? Value(nodes, branch, depth + 1) : null;
        }

        // A switch passes through the first of its inputs that has anything in
        // it; the rest of its branches were not taken.
        if (kind.Contains("Switch", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var input in inputs.EnumerateObject()
                         .Where(p => p.Name.StartsWith("any", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
                if (Value(nodes, input.Value, depth + 1) is { } taken && taken.Trim().Length > 0)
                    return taken;

            return null;
        }

        // A plain source of words: a string primitive, a LoRA name, an encoder
        // taking its text from elsewhere.
        if (TextOf(inputs) is { Length: > 0 } written) return written;

        foreach (var name in new[] { "text", "string", "value", "lora_name" })
            if (inputs.TryGetProperty(name, out var carried))
                return Value(nodes, carried, depth + 1);

        return null;
    }

    /// <summary>A literal, or whatever the node it names produces.</summary>
    private static string? Value(IReadOnlyDictionary<string, JsonElement> nodes,
                                 JsonElement value, int depth)
    {
        if (depth > 32) return null;

        if (value.ValueKind == JsonValueKind.String) return value.GetString();

        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0) return null;

        var id = value[0].ValueKind == JsonValueKind.String ? value[0].GetString() : value[0].ToString();
        return id is null ? null : Assembled(nodes, id, depth + 1);
    }

    /// <summary>
    /// A regular-expression replacement, as the node would perform it.
    /// </summary>
    /// <remarks>
    /// These nodes are Python, so a backreference in the replacement is written
    /// <c>\1</c>; .NET spells it <c>$1</c>. The pattern itself is somebody
    /// else's and arrives from a file, so it runs under a time limit — a
    /// pattern that backtracks forever would otherwise hang the window that
    /// asked what an image was made with.
    /// </remarks>
    private static string? Replaced(IReadOnlyDictionary<string, JsonElement> nodes,
                                    JsonElement inputs, int depth)
    {
        if (!inputs.TryGetProperty("text", out var source)) return null;
        if (Value(nodes, source, depth + 1) is not { } text) return null;

        var pattern = Text(inputs, "search");
        if (string.IsNullOrEmpty(pattern)) return text;

        var replacement = System.Text.RegularExpressions.Regex.Replace(
            Text(inputs, "replacement") ?? string.Empty, @"\\(\d)", "$$$1");

        var options = System.Text.RegularExpressions.RegexOptions.None;
        if (!Flag(inputs, "case_sensitive"))
            options |= System.Text.RegularExpressions.RegexOptions.IgnoreCase;

        var limit = int.TryParse(Number(inputs, "max_replacements"), out var max) && max > 0 ? max : -1;

        try
        {
            var regex = new System.Text.RegularExpressions.Regex(
                pattern, options, TimeSpan.FromMilliseconds(250));

            var result = regex.Replace(text, replacement, limit);

            // "Keep text" is what these nodes do when the pattern matches
            // nothing, and it is also the only sensible answer here.
            return result.Length > 0 ? result : text;
        }
        catch
        {
            return text;
        }
    }

    /// <summary>What a condition node answers.</summary>
    private static bool Truth(IReadOnlyDictionary<string, JsonElement> nodes,
                              JsonElement inputs, int depth)
    {
        if (!inputs.TryGetProperty("cond", out var cond)) return false;

        if (cond.ValueKind is JsonValueKind.True or JsonValueKind.False) return cond.GetBoolean();
        if (cond.ValueKind != JsonValueKind.Array || cond.GetArrayLength() == 0) return false;

        var id = cond[0].ValueKind == JsonValueKind.String ? cond[0].GetString() : cond[0].ToString();
        if (id is null || !nodes.TryGetValue(id, out var node) || Inputs(node) is not { } test) return false;

        if (!Class(node).Contains("Contains", StringComparison.OrdinalIgnoreCase)) return false;

        var haystack = test.TryGetProperty("string", out var s) ? Value(nodes, s, depth + 1) : null;
        var needle = Text(test, "substring");

        if (haystack is null || string.IsNullOrEmpty(needle)) return false;

        return haystack.Contains(needle, Flag(test, "case_sensitive")
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase);
    }

    private static bool Flag(JsonElement inputs, string name) =>
        inputs.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

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
                                        IReadOnlyDictionary<string, string> cached,
                                        JsonElement inputs, string slot, string polarity, int depth)
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

        // Nothing written in the node itself, so work out what it produces:
        // follow the recipe the graph records and see what comes out.
        if (Assembled(nodes, id, depth) is { Length: > 0 } built) return built;

        // Failing that, what the node's own box last held. Last, because it is
        // the last value rather than a record of this run — a workflow run
        // twice with different inputs leaves the first run's words sitting in
        // the box, and believing them means reporting the wrong prompt with
        // every appearance of certainty.
        if (cached.GetValueOrDefault(id) is { Length: > 0 } remembered) return remembered;

        // Not a text node: try whatever it is taking its own conditioning —
        // or its text — from. A prompt reaches the sampler through combine,
        // style, guidance and guider nodes, and through wildcard nodes that
        // take the words from a string node somewhere else again.
        //
        // Only ever through its own side. A guider node names both prompts, so
        // a walk that would step into either took whichever was listed first
        // and reported the negative prompt as the positive one.
        foreach (var candidate in Carriers)
        {
            if (candidate is "positive" or "negative" && candidate != polarity) continue;

            if (FollowToText(nodes, cached, next.Value, candidate, polarity, depth + 1) is { } found)
                return found;
        }

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
