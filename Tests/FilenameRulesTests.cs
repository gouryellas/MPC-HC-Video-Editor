using System.Linq;
using MpcHcVideoEditor.Services;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// Every rule, held to one example each.
/// </summary>
/// <remarks>
/// The names here are invented. Nothing from a real library belongs in a
/// repository.
/// </remarks>
public class FilenameRulesTests
{
    [Theory]
    // Runs of spaces collapse to one, then every space becomes a dash.
    [InlineData("wedding vows.mp4", "wedding-vows.mp4")]
    [InlineData("wedding  vows.mp4", "wedding-vows.mp4")]
    [InlineData("wedding   vows.mp4", "wedding-vows.mp4")]
    [InlineData("wedding    vows.mp4", "wedding-vows.mp4")]
    public void Spaces_collapse_then_become_dashes(string original, string expected)
    {
        Assert.Equal(expected, FilenameRules.Apply(original));
    }


    [Fact]
    public void Everything_goes_lowercase()
    {
        Assert.Equal("wedding-vows.mp4", FilenameRules.Apply("WEDDING VOWS.MP4"));
    }

    [Theory]
    [InlineData("abcdefghijklmnopqrstuvwxyz.mp4")]
    [InlineData("0123456789.mp4")]
    [InlineData("a-b.mp4")]
    [InlineData("a_b.mp4")]
    [InlineData("a(b).mp4")]
    [InlineData("a[b].mp4")]
    public void The_allowed_characters_all_survive(string name)
    {
        Assert.Equal(name, FilenameRules.Apply(name));
    }

    [Fact]
    public void An_empty_pair_of_brackets_is_dropped()
    {
        // A tag with nothing in it says nothing. The brackets are allowed
        // characters, but an empty pair is punctuation around a hole.
        Assert.Equal("clip.mp4", FilenameRules.Apply("clip[].mp4"));
    }

    [Theory]
    [InlineData("!")] [InlineData("@")] [InlineData("#")] [InlineData("$")]
    [InlineData("%")] [InlineData("^")] [InlineData("&")] [InlineData("*")]
    [InlineData("+")] [InlineData("=")] [InlineData("{")] [InlineData("}")]
    [InlineData("|")] [InlineData(";")] [InlineData("\"")] [InlineData("'")]
    [InlineData("<")] [InlineData(">")] [InlineData(",")] [InlineData("?")]
    [InlineData("`")] [InlineData("~")]
    public void A_disallowed_character_between_two_words_divides_them(string bad)
    {
        Assert.Equal("a-b.mp4", FilenameRules.Apply($"a{bad}b.mp4"));
    }

    [Theory]
    [InlineData("!clip.mp4")]
    [InlineData("clip!.mp4")]
    [InlineData("!!clip!!.mp4")]
    [InlineData("~ clip ~.mp4")]
    public void A_disallowed_character_dividing_nothing_is_deleted(string original)
    {
        // At either end it was separating a word from nothing, so there is
        // no gap for a dash to stand in.
        Assert.Equal("clip.mp4", FilenameRules.Apply(original));
    }

    [Theory]
    [InlineData("a((((b.mp4", "a(b.mp4")]
    [InlineData("a[[b.mp4", "a[b.mp4")]
    [InlineData("a---b.mp4", "a-b.mp4")]
    [InlineData("a___b.mp4", "a_b.mp4")]
    [InlineData("a...b.mp4", "a-b.mp4")]
    public void No_punctuation_character_may_repeat(string original, string expected)
    {
        Assert.Equal(expected, FilenameRules.Apply(original));
    }

    [Theory]
    [InlineData("bookkeeper.mp4")]
    [InlineData("soccer.mp4")]
    [InlineData("vol2000.mp4")]
    public void Repeated_letters_and_digits_are_left_alone(string name)
    {
        // Not in the instruction, but it has to be: collapsing these would
        // turn "bookkeeper" into "bokeper" and 2000 into 20.
        Assert.Equal(name, FilenameRules.Apply(name));
    }

    [Fact]
    public void A_dot_inside_the_name_goes_but_the_extension_keeps_its_own()
    {
        // A disallowed character between two kept ones was dividing them,
        // so it becomes a dash rather than joining the words together.
        Assert.Equal("vol2-take3.mp4", FilenameRules.Apply("vol2.take3.mp4"));
    }

    [Fact]
    public void Accents_come_down_to_plain_letters()
    {
        // Deleting them would leave a word that cannot be searched for.
        Assert.Equal("tyna-jose-renee.mp4", FilenameRules.Apply("Týna José Renée.mp4"));
    }

    [Theory]
    [InlineData("ø", "o")]
    [InlineData("æ", "ae")]
    [InlineData("ß", "ss")]
    [InlineData("ł", "l")]
    public void Letters_whose_mark_is_part_of_their_shape_are_spelled_out(string letter, string plain)
    {
        // These do not decompose, so stripping accents alone would delete
        // them outright.
        Assert.Equal($"a{plain}b.mp4", FilenameRules.Apply($"a{letter}b.mp4"));
    }

    [Fact]
    public void A_script_with_no_plain_equivalent_leaves_the_file_alone()
    {
        var proposal = Assert.Single(FilenameRules.Propose(new[] { "Привет.mp4" }));

        Assert.Equal("Привет.mp4", proposal.Proposed);
        Assert.False(proposal.Changes);
        Assert.Contains("No allowed characters", proposal.Note);
    }

    [Fact]
    public void Deleting_a_character_does_not_leave_a_double_dash()
    {
        // "a , b" would otherwise become "a--b".
        Assert.Equal("a-b.mp4", FilenameRules.Apply("a , b.mp4"));
    }

    [Fact]
    public void Leading_and_trailing_dashes_are_trimmed()
    {
        Assert.Equal("clip.mp4", FilenameRules.Apply("  clip  .mp4"));
    }

    [Fact]
    public void A_tag_is_kept_and_moved_to_the_end()
    {
        Assert.Equal("wedding-vows[take1].mp4",
                     FilenameRules.Apply("wedding [take1] vows.mp4"));
    }

    [Fact]
    public void An_existing_first_last_pair_keeps_its_underscore()
    {
        Assert.Equal("sarah_williams-cries.mp4",
                     FilenameRules.Apply("sarah_williams cries.mp4"));
    }

    [Fact]
    public void A_name_already_in_its_slot_stays_there()
    {
        // No vocabulary, so nothing marks "wedding" as a category — but the
        // name is third, and the two words in front of it are therefore the
        // category and the subject. Pulling the name forward would break a
        // correct name on the strength of an empty vocabulary.
        Assert.Equal("wedding-vows-sarah_williams-cries.mp4",
                     FilenameRules.Apply("wedding-vows-sarah_williams-cries.mp4"));
    }

    [Fact]
    public void A_name_reaches_the_front_only_with_nothing_in_front_of_it()
    {
        Assert.Equal("sarah_williams-cries.mp4",
                     FilenameRules.Apply("sarah_williams cries.mp4"));
    }

    [Fact]
    public void Words_in_front_of_a_name_fill_the_slots_before_it()
    {
        // One word in front fills the category; the subject stays empty,
        // which is one of the valid shapes.
        Assert.Equal("wedding-sarah_williams-cries.mp4",
                     FilenameRules.Apply("wedding sarah_williams cries.mp4"));
    }

    [Fact]
    public void More_words_than_slots_push_the_rest_into_the_description()
    {
        Assert.Equal("a-b-sarah_williams-c-d.mp4",
                     FilenameRules.Apply("a b c sarah_williams d.mp4"));
    }

    [Fact]
    public void Without_a_vocabulary_the_words_keep_their_order()
    {
        // Guessing which word is the category would produce a name that
        // looks deliberate and is wrong.
        Assert.Equal("one-two-three.mp4", FilenameRules.Apply("one two three.mp4"));
    }

    [Fact]
    public void A_vocabulary_puts_the_sections_in_order()
    {
        var vocab = new FilenameVocabulary
        {
            Categories = new[] { "wedding" },
            Subjects = new[] { "vows" },
            FirstNames = new[] { "sarah" }
        };

        Assert.Equal("wedding-vows-sarah_williams-cries[take1].mp4",
                     FilenameRules.Apply("cries sarah williams [take1] vows wedding.mp4", vocab));
    }

    [Fact]
    public void A_known_first_name_takes_the_word_after_it_as_the_surname()
    {
        var vocab = new FilenameVocabulary { FirstNames = new[] { "sarah" } };

        Assert.Equal("sarah_williams-dancing.mp4",
                     FilenameRules.Apply("sarah williams dancing.mp4", vocab));
    }

    [Fact]
    public void A_lone_first_name_is_still_a_name()
    {
        var vocab = new FilenameVocabulary { FirstNames = new[] { "sarah" } };

        // A name with nothing after it is still a name, and the word in
        // front of it takes the category slot rather than being shoved
        // behind it.
        Assert.Equal("dancing-sarah.mp4",
                     FilenameRules.Apply("dancing sarah.mp4", vocab));
    }

    [Fact]
    public void Multi_word_sections_are_joined_with_dashes()
    {
        // The example from the rules: a section that needs more than one
        // word uses dashes, never a space and never an underscore.
        var result = FilenameRules.Apply("wedding after party dancing the night [1st].mp4");

        Assert.Equal("wedding-after-party-dancing-the-night[1st].mp4", result);
        Assert.DoesNotContain(" ", result);
    }

    [Fact]
    public void The_underscore_is_not_spent_on_anything_but_a_name()
    {
        var result = FilenameRules.Apply("wedding after party.mp4");

        Assert.DoesNotContain("_", result);
    }

    [Fact]
    public void Applying_the_rules_twice_changes_nothing_the_second_time()
    {
        // The preview is editable, and whatever is typed goes through these
        // again — so a name that has already been through them must come
        // back unchanged or the field would fight the typist.
        const string messy = "WEDDING  Vows!! Sarah Williams [Take 1].MP4";

        var once = FilenameRules.Apply(messy);

        Assert.Equal(once, FilenameRules.Apply(once));
    }

    [Fact]
    public void Two_files_that_would_share_a_name_are_flagged_not_renumbered()
    {
        var proposals = FilenameRules.Propose(new[] { "clip one.mp4", "CLIP  ONE.mp4" });

        Assert.Equal("clip-one.mp4", proposals[0].Proposed);
        Assert.Equal("", proposals[0].Note);

        Assert.Equal("clip-one.mp4", proposals[1].Proposed);
        Assert.Contains("already takes this name", proposals[1].Note);
    }

    [Fact]
    public void A_file_already_in_the_house_style_is_left_untouched()
    {
        // With no vocabulary at all, which is the harder case: the shape
        // alone has to be enough to leave it alone.
        var proposal = Assert.Single(
            FilenameRules.Propose(new[] { "wedding-vows-sarah_williams-cries[take1].mp4" }));

        Assert.False(proposal.Changes);
        Assert.Equal("", proposal.Note);
    }

    [Fact]
    public void The_extension_is_cleaned_too()
    {
        Assert.Equal("clip.mp4", FilenameRules.Apply("clip.MP4"));
    }
}
