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
            Words = new[] { "wedding", "vows" },
            Rules = new[]
            {
                new RenameRule
                {
                    If = new List<string> { "vows" },
                    Then = new List<string> { "wedding", "vows" }
                }
            }
        };

        // The name is recognised by its underscore, the other two by the
        // vocabulary, and each goes to its own slot.
        Assert.Equal("wedding-vows-sarah_williams-cries[take1].mp4",
                     FilenameRules.Apply("cries sarah_williams [take1] vows wedding.mp4", vocab));
    }

    [Fact]
    public void A_rule_leads_with_words_the_name_does_not_have()
    {
        // The point of a rule: neither "football" nor "college" appears in
        // the name, and both belong in front of it.
        var vocab = new FilenameVocabulary
        {
            Words = new[] { "aggies", "football", "college" },
            Rules = new[]
            {
                new RenameRule
                {
                    If = new List<string> { "aggies" },
                    Then = new List<string> { "football", "college", "aggies" }
                }
            }
        };

        Assert.Equal("football-college-aggies-highlights.mp4",
                     FilenameRules.Apply("aggies highlights.mp4", vocab));
    }

    [Fact]
    public void A_condition_may_want_two_words_at_once()
    {
        var vocab = new FilenameVocabulary
        {
            Rules = new[]
            {
                new RenameRule
                {
                    If = new List<string> { "aggies", "practice" },
                    Then = new List<string> { "football", "practice" }
                }
            }
        };

        Assert.Equal("football-practice-aggies.mp4",
                     FilenameRules.Apply("aggies practice.mp4", vocab));

        // One of the two is not both.
        Assert.Equal("aggies-highlights.mp4",
                     FilenameRules.Apply("aggies highlights.mp4", vocab));
    }

    [Fact]
    public void The_otherwise_order_is_used_when_the_condition_fails()
    {
        var vocab = new FilenameVocabulary
        {
            Rules = new[]
            {
                new RenameRule
                {
                    If = new List<string> { "aggies" },
                    Then = new List<string> { "football" },
                    Else = new List<string> { "other" }
                }
            }
        };

        Assert.Equal("football-aggies.mp4", FilenameRules.Apply("aggies.mp4", vocab));
        Assert.Equal("other-longhorns.mp4", FilenameRules.Apply("longhorns.mp4", vocab));
    }

    [Fact]
    public void A_word_the_rule_names_is_not_left_in_twice()
    {
        var vocab = new FilenameVocabulary
        {
            Rules = new[]
            {
                new RenameRule
                {
                    If = new List<string> { "aggies" },
                    Then = new List<string> { "football", "aggies" }
                }
            }
        };

        Assert.Equal("football-aggies-practice.mp4",
                     FilenameRules.Apply("football aggies practice.mp4", vocab));
    }

    [Fact]
    public void The_first_rule_with_something_to_say_decides()
    {
        var vocab = new FilenameVocabulary
        {
            Rules = new[]
            {
                new RenameRule
                {
                    If = new List<string> { "aggies" },
                    Then = new List<string> { "football" }
                },
                new RenameRule
                {
                    If = new List<string> { "aggies" },
                    Then = new List<string> { "basketball" }
                }
            }
        };

        Assert.Equal("football-aggies.mp4", FilenameRules.Apply("aggies.mp4", vocab));
    }

    [Fact]
    public void A_rule_with_no_ordering_says_nothing()
    {
        var vocab = new FilenameVocabulary
        {
            Rules = new[] { new RenameRule { If = new List<string> { "aggies" } } }
        };

        Assert.Equal("aggies-highlights.mp4",
                     FilenameRules.Apply("aggies highlights.mp4", vocab));
    }

    [Fact]
    public void Empty_slots_in_a_rule_are_ignored()
    {
        // The window always hands over three slots; two of them are usually
        // blank, and a blank is not a word to look for.
        var vocab = new FilenameVocabulary
        {
            Rules = new[]
            {
                new RenameRule
                {
                    If = new List<string> { "aggies", "", "" },
                    Then = new List<string> { "football", "", "" }
                }
            }
        };

        Assert.Equal("football-aggies.mp4", FilenameRules.Apply("aggies.mp4", vocab));
    }

    [Fact]
    public void A_blacklisted_word_is_struck_out()
    {
        var vocab = new FilenameVocabulary { Blacklist = new[] { "raw" } };

        Assert.Equal("wedding-vows.mp4",
                     FilenameRules.Apply("wedding raw vows.mp4", vocab));
    }

    [Fact]
    public void A_blacklisted_word_only_matches_a_whole_word()
    {
        // Striking a run of letters wherever it appeared would quietly
        // damage words nobody meant to touch.
        var vocab = new FilenameVocabulary { Blacklist = new[] { "cam" } };

        Assert.Equal("camera-cameron.mp4",
                     FilenameRules.Apply("cam camera cameron.mp4", vocab));
    }

    [Fact]
    public void Blacklisting_everything_leaves_the_file_alone()
    {
        // Nothing left to call it, so it keeps the name it has rather than
        // becoming ".mp4".
        var vocab = new FilenameVocabulary { Blacklist = new[] { "raw" } };

        Assert.Equal("raw.mp4", FilenameRules.Apply("raw.mp4", vocab));
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

    /// <summary>One rule, written as lines rather than the old four lists.</summary>
    private static FilenameVocabulary Vocabulary(params RuleLine[] lines) =>
        new() { Rules = new[] { new RenameRule { Lines = lines.ToList() } } };

    [Fact]
    public void A_rule_can_ask_that_a_word_is_absent()
    {
        var vocab = Vocabulary(
            new RuleLine(RuleActions.IfNotFound, new[] { "practice" }),
            new RuleLine(RuleActions.Order, new[] { "game" }));

        Assert.Equal("game-aggies.mp4", FilenameRules.Apply("aggies.mp4", vocab));

        // Present, so the rule has nothing to say and the name is left as
        // the plain rules make it.
        Assert.Equal("aggies-practice.mp4", FilenameRules.Apply("aggies practice.mp4", vocab));
    }

    [Fact]
    public void Two_conditions_both_have_to_hold()
    {
        // Across lines it is always "and"; "or" is something said inside one
        // line, between its own words.
        var vocab = Vocabulary(
            new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
            new RuleLine(RuleActions.IfNotFound, new[] { "practice" }),
            new RuleLine(RuleActions.Order, new[] { "football" }));

        Assert.Equal("football-aggies.mp4", FilenameRules.Apply("aggies.mp4", vocab));
        Assert.Equal("aggies-practice.mp4", FilenameRules.Apply("aggies practice.mp4", vocab));
    }

    [Fact]
    public void A_rule_can_delete_words()
    {
        // Unlike the blacklist, which strikes a word out of every name: this
        // one only applies where the condition holds.
        var vocab = Vocabulary(
            new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
            new RuleLine(RuleActions.Delete, new[] { "raw", "temp" }));

        Assert.Equal("aggies-highlights.mp4",
                     FilenameRules.Apply("aggies raw highlights temp.mp4", vocab));

        Assert.Equal("longhorns-raw.mp4",
                     FilenameRules.Apply("longhorns raw.mp4", vocab));
    }

    [Fact]
    public void A_rule_can_change_one_word_into_another()
    {
        var vocab = Vocabulary(
            new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
            new RuleLine(RuleActions.Change, new[] { "hilites", "vid" }),
            new RuleLine(RuleActions.ChangeTo, new[] { "highlights", "video" }));

        Assert.Equal("aggies-highlights-video.mp4",
                     FilenameRules.Apply("aggies hilites vid.mp4", vocab));
    }

    [Fact]
    public void A_change_pairs_by_position_not_by_order_written()
    {
        // The second box changes into the second box underneath it, even
        // with the first pair left blank — which is what the two rows
        // sitting one above the other look like they promise.
        var vocab = Vocabulary(
            new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
            new RuleLine(RuleActions.Change, new[] { "", "vid", "" }),
            new RuleLine(RuleActions.ChangeTo, new[] { "", "video", "" }));

        Assert.Equal("aggies-vid.mp4", FilenameRules.Apply("aggies vid.mp4",
            Vocabulary(
                new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
                new RuleLine(RuleActions.Change, new[] { "vid" }))));

        Assert.Equal("aggies-video.mp4", FilenameRules.Apply("aggies vid.mp4", vocab));
    }

    [Fact]
    public void A_half_finished_change_leaves_the_word_alone()
    {
        // Rather than deleting it. A blank on one side is a pair somebody
        // has not finished typing, not a second way to say "delete these".
        var vocab = Vocabulary(
            new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
            new RuleLine(RuleActions.Change, new[] { "vid" }),
            new RuleLine(RuleActions.ChangeTo, Array.Empty<string>()));

        Assert.Equal("aggies-vid.mp4", FilenameRules.Apply("aggies vid.mp4", vocab));
    }

    [Fact]
    public void Every_rule_that_holds_gets_to_edit_but_only_one_orders()
    {
        var vocab = new FilenameVocabulary
        {
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
                        new RuleLine(RuleActions.Order, new[] { "football" })
                    }
                },
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
                        new RuleLine(RuleActions.Order, new[] { "basketball" }),
                        new RuleLine(RuleActions.Delete, new[] { "raw" })
                    }
                }
            }
        };

        // Every rule that can apply does: the first ordering wins, and
        // the second rule's deletion still happens.
        Assert.Equal("football-aggies.mp4", FilenameRules.Apply("aggies raw.mp4", vocab));
    }

    [Fact]
    public void A_rule_that_does_not_hold_passes_the_file_down()
    {
        var vocab = new FilenameVocabulary
        {
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "cyclones" }),
                        new RuleLine(RuleActions.Order, new[] { "basketball" })
                    }
                },
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
                        new RuleLine(RuleActions.Order, new[] { "football" }),
                        new RuleLine(RuleActions.Delete, new[] { "raw" })
                    }
                }
            }
        };

        Assert.Equal("football-aggies.mp4",
                     FilenameRules.Apply("aggies raw.mp4", vocab));
    }

    [Fact]
    public void A_file_no_rule_recognises_gets_the_general_treatment()
    {
        var vocab = new FilenameVocabulary
        {
            Blacklist = new[] { "raw" },
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "cyclones" }),
                        new RuleLine(RuleActions.Order, new[] { "basketball" })
                    }
                }
            }
        };

        // No rule applies: cleanup, the blacklist and the found order
        // carry it.
        Assert.Equal("aggies-highlights.mp4",
                     FilenameRules.Apply("aggies highlights raw.mp4", vocab));
    }

    [Theory]
    // The second shape is the one that was silently failing: the cleanup
    // welds "ab_clip" into one name-pair word, the condition sees "ab" in
    // the original and fires — and the deletion then has to find the word
    // inside the pair, or the rule fires and changes nothing.
    [InlineData("_ab_clip.mp4")]
    [InlineData("ab_clip.mp4")]
    [InlineData("ab clip.mp4")]
    public void Delete_the_found_words_removes_the_word_wherever_cleanup_put_it(string original)
    {
        var vocab = new FilenameVocabulary
        {
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "ab" }),
                        new RuleLine(RuleActions.Order, new[] { "xy" }),
                        new RuleLine(RuleActions.DeleteFound)
                    }
                }
            }
        };

        Assert.Equal("xy-clip.mp4", FilenameRules.Apply(original, vocab));
    }

    [Fact]
    public void Delete_these_reaches_into_a_joined_pair_too()
    {
        var vocab = new FilenameVocabulary
        {
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "ab" }),
                        new RuleLine(RuleActions.Delete, new[] { "ab" })
                    }
                }
            }
        };

        Assert.Equal("clip.mp4", FilenameRules.Apply("ab_clip.mp4", vocab));
    }

    [Fact]
    public void The_blacklist_scrubs_after_the_rules_have_had_their_say()
    {
        // A rule swaps a word in; the blacklist knows that word. Rules
        // first, then the general scrub — so the swapped-in word is
        // scrubbed like anything else.
        var vocab = new FilenameVocabulary
        {
            Blacklist = new[] { "sample" },
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "aggies" }),
                        new RuleLine(RuleActions.Change, new[] { "demo" }),
                        new RuleLine(RuleActions.ChangeTo, new[] { "sample" })
                    }
                }
            }
        };

        Assert.Equal("aggies.mp4", FilenameRules.Apply("aggies demo.mp4", vocab));
    }

    [Theory]
    // Everywhere a space would become a dash, so does an underscore.
    [InlineData("_leading.mp4", "leading.mp4")]
    [InlineData("trailing_.mp4", "trailing.mp4")]
    [InlineData("a___b.mp4", "a-b.mp4")]
    [InlineData("wedding_vows__take2.mp4", "wedding_vows-take2.mp4")]
    [InlineData("a_ b.mp4", "a-b.mp4")]
    // Except the one standing between two words, which is the only mark
    // a name has.
    [InlineData("sarah_williams.mp4", "sarah_williams.mp4")]
    [InlineData("wedding-vows-sarah_williams.mp4", "wedding-vows-sarah_williams.mp4")]
    public void An_underscore_separates_like_a_space_unless_it_joins_a_name(
        string original, string expected)
    {
        Assert.Equal(expected, FilenameRules.Apply(original));
    }

    [Theory]
    // A file that opens with an underscore writes its separators as
    // underscores, so the next one is not half of a name. Before this,
    // "_ab_title" lost its leading mark and then welded the rest into
    // one word "ab_title" that no word list would ever find.
    [InlineData("_ab_title.mp4", "ab-title.mp4")]
    [InlineData("x__a_b.mp4", "x-a-b.mp4")]
    // A name is a pair and only a pair: the underscore that made one is
    // itself evidence, so a chain stops at two.
    [InlineData("a_b_c.mp4", "a_b-c.mp4")]
    // A space between puts the question back open — the next single
    // underscore after clean separators may still be a name.
    [InlineData("a_ b c_d.mp4", "a-b-c_d.mp4")]
    public void An_underscore_that_did_not_join_unmasks_the_ones_after_it(
        string original, string expected)
    {
        Assert.Equal(expected, FilenameRules.Apply(original));
    }

    [Fact]
    public void A_condition_is_asked_about_the_name_as_it_was_typed()
    {
        // The cleanup keeps sarah_williams as one word, which is right
        // for the output — but the person who wrote this rule was looking
        // at a filename that plainly says williams, and the condition has
        // to see what they saw.
        var vocab = new FilenameVocabulary
        {
            Words = new List<string> { "williams", "wedding" },
            Rules = new List<RenameRule>
            {
                new()
                {
                    Lines = new List<RuleLine>
                    {
                        new(RuleActions.IfFound, new List<string> { "williams" }),
                        new(RuleActions.Order, new List<string> { "wedding" }),
                    }
                }
            }
        };

        Assert.Equal("wedding-sarah_williams-vows.mp4",
                     FilenameRules.Apply("sarah_williams vows.mp4", vocab));
    }

    [Fact]
    public void A_condition_may_name_the_pair_or_either_half()
    {
        var vocab = new FilenameVocabulary
        {
            Words = new List<string> { "sarah_williams", "wedding" },
            Rules = new List<RenameRule>
            {
                new()
                {
                    Lines = new List<RuleLine>
                    {
                        new(RuleActions.IfFound, new List<string> { "sarah_williams" }),
                        new(RuleActions.Order, new List<string> { "wedding" }),
                    }
                }
            }
        };

        Assert.Equal("wedding-sarah_williams-vows.mp4",
                     FilenameRules.Apply("sarah_williams vows.mp4", vocab));
    }

    [Theory]
    // Whatever the original fenced its words with, the condition sees
    // the words. The first of these is the shape that started it all:
    // cleaned on its own, "_ab_clip" once welded into "ab_clip".
    [InlineData("_ab_clip.mp4")]
    [InlineData("ab.clip.mp4")]
    [InlineData("AB (clip).mp4")]
    public void No_separator_style_hides_a_word_from_a_condition(string original)
    {
        var vocab = new FilenameVocabulary
        {
            Words = new List<string> { "ab", "action" },
            Rules = new List<RenameRule>
            {
                new()
                {
                    Lines = new List<RuleLine>
                    {
                        new(RuleActions.IfFound, new List<string> { "ab" }),
                        new(RuleActions.Order, new List<string> { "action" }),
                    }
                }
            }
        };

        Assert.StartsWith("action-", FilenameRules.Apply(original, vocab));
    }

    [Theory]
    // The two the request named, and what each is expected to catch.
    [InlineData("go", "goes")]
    [InlineData("go", "going")]
    [InlineData("go", "gone")]
    [InlineData("go", "went")]
    [InlineData("be", "am")]
    [InlineData("be", "is")]
    [InlineData("be", "are")]
    [InlineData("be", "was")]
    [InlineData("be", "were")]
    [InlineData("be", "being")]
    [InlineData("be", "been")]
    // And a few of the rest, in both directions.
    [InlineData("run", "ran")]
    [InlineData("swam", "swim")]
    [InlineData("children", "child")]
    [InlineData("mice", "mouse")]
    public void Irregular_catches_the_forms_no_ending_explains(string a, string b)
    {
        Assert.True(WordMatch.Irregulars.Equals(a, b), $"{a} should match {b}");
        Assert.True(WordMatch.Irregulars.Equals(b, a), $"{b} should match {a}");

        Assert.Equal(WordMatch.Irregulars.GetHashCode(a), WordMatch.Irregulars.GetHashCode(b));
    }

    [Fact]
    public void Each_mode_reaches_further_than_the_one_before()
    {
        // Precise is the word as written.
        Assert.True(WordMatch.Exact.Equals("wave", "wave"));
        Assert.False(WordMatch.Exact.Equals("wave", "waves"));

        // Variant adds the endings, but cannot reach a word that
        // changes shape.
        Assert.True(WordMatch.Variants.Equals("wave", "waves"));
        Assert.False(WordMatch.Variants.Equals("go", "went"));

        // Irregular is everything Variant does, and those as well.
        Assert.True(WordMatch.Irregulars.Equals("wave", "waving"));
        Assert.True(WordMatch.Irregulars.Equals("go", "went"));
    }

    [Fact]
    public void A_word_too_short_to_stem_needs_the_irregular_list()
    {
        // Three letters is the floor: below it, taking an ending off
        // leaves something that collides with everything. So "goes" does
        // not reach "go" by rule — it reaches it by being listed.
        Assert.False(WordMatch.Variants.Equals("go", "goes"));
        Assert.True(WordMatch.Irregulars.Equals("go", "goes"));
    }

    [Fact]
    public void Unrelated_words_are_not_dragged_in_by_a_family()
    {
        Assert.False(WordMatch.Irregulars.Equals("go", "be"));
        Assert.False(WordMatch.Irregulars.Equals("went", "gotten"));
        Assert.False(WordMatch.Irregulars.Equals("mouse", "moose"));
    }

    [Theory]
    [InlineData("beach|shore", "beach", true)]
    [InlineData("beach|shore", "shore", true)]
    [InlineData("beach|shore", "beaches", false)]
    [InlineData("beach.*", "beaches", true)]
    [InlineData("20[0-9]{2}", "2026", true)]
    [InlineData("20[0-9]{2}", "1999", false)]
    // Anchored, so a pattern cannot match a word that merely contains it.
    [InlineData("beach", "beachfront", false)]
    // A half-typed expression matches nothing rather than throwing.
    [InlineData("beach(", "beach", false)]
    public void Regex_matches_a_whole_word(string pattern, string word, bool expected)
    {
        var match = WordMatch.For(WordMatchMode.Variant, regex: true);

        Assert.Equal(expected, match.Equals(pattern, word));
    }

    [Fact]
    public void A_rule_can_be_written_as_an_expression()
    {
        var vocab = new FilenameVocabulary
        {
            UseRegex = true,
            Words = new[] { "ferrari|ford|chevrolet", "automobile" },
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "ferrari|ford|chevrolet" }),
                        new RuleLine(RuleActions.Order, new[] { "automobile" }),
                        new RuleLine(RuleActions.DeleteFound)
                    }
                }
            }
        };

        Assert.Equal("automobile-red-1998.mp4",
                     FilenameRules.Apply("ferrari red 1998.mp4", vocab));

        Assert.Equal("automobile-barn-find.mp4",
                     FilenameRules.Apply("barn find chevrolet.mp4", vocab));

        Assert.Equal("red-bicycle.mp4", FilenameRules.Apply("red bicycle.mp4", vocab));
    }

    [Fact]
    public void A_condition_can_ask_about_a_blacklisted_word()
    {
        // The blacklist strikes a word out of the name; it does not hide it
        // from the rules. Somebody writing "if temp is NOT found" is exactly
        // the person who also blacklisted "temp", and without this that
        // condition would be quietly true for every file.
        var vocab = new FilenameVocabulary
        {
            Blacklist = new[] { "temp" },
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "skiing" }),
                        new RuleLine(RuleActions.IfNotFound, new[] { "temp" }),
                        new RuleLine(RuleActions.Order, new[] { "vacation" })
                    }
                }
            }
        };

        Assert.Equal("vacation-skiing-lodge.mp4",
                     FilenameRules.Apply("skiing lodge.mp4", vocab));

        // Present, so the rule stands down — and "temp" is still struck out
        // of the name by the blacklist either way.
        Assert.Equal("skiing-cut.mp4", FilenameRules.Apply("skiing temp cut.mp4", vocab));
    }

    [Theory]
    // The two the request named, each written once and expected to catch
    // every form of itself.
    [InlineData("wave", "waves")]
    [InlineData("wave", "waving")]
    [InlineData("wave", "waved")]
    [InlineData("bounce", "bounces")]
    [InlineData("bounce", "bouncing")]
    [InlineData("bounce", "bounced")]
    [InlineData("bounce", "bouncy")]
    [InlineData("bounce", "bounciest")]
    [InlineData("bounce", "bouncier")]
    // Either way round: the list may hold the variant and the filename the
    // plain word.
    [InlineData("waving", "waved")]
    [InlineData("bouncing", "bounciest")]
    // The spelling changes that come with an ending.
    [InlineData("run", "running")]
    [InlineData("stop", "stopped")]
    [InlineData("party", "parties")]
    [InlineData("box", "boxes")]
    [InlineData("dish", "dishes")]
    [InlineData("watch", "watches")]
    [InlineData("class", "classes")]
    public void One_word_stands_for_its_endings(string a, string b)
    {
        Assert.True(WordMatch.Variants.Equals(a, b), $"{a} should match {b}");
        Assert.True(WordMatch.Variants.Equals(b, a), $"{b} should match {a}");

        // And the hash has to agree, or Contains and Distinct will not see
        // what Equals sees.
        Assert.Equal(WordMatch.Variants.GetHashCode(a), WordMatch.Variants.GetHashCode(b));
    }

    [Theory]
    // Words close enough to be folded together by a careless stemmer, and
    // which have to stay apart.
    [InlineData("move", "movie")]
    [InlineData("wedding", "wedge")]
    [InlineData("beach", "bench")]
    [InlineData("video", "vide")]
    [InlineData("college", "collage")]
    [InlineData("watch", "water")]
    public void Different_words_are_not_folded_together(string a, string b)
    {
        Assert.False(WordMatch.Variants.Equals(a, b), $"{a} should not match {b}");
    }

    [Fact]
    public void Very_short_words_are_folded_together_and_that_is_the_deal()
    {
        // "cut" has to reach the same stem as cuts and cutting, which means
        // stripping down to three letters — and "cute" losing its silent e
        // lands in the same place. There is no suffix rule that separates
        // them, so this is written down as expected rather than left as a
        // surprise. The checkbox is the way out.
        Assert.True(WordMatch.Variants.Equals("cut", "cute"));
        Assert.False(WordMatch.Exact.Equals("cut", "cute"));
    }

    [Fact]
    public void Endings_can_be_turned_off()
    {
        Assert.True(WordMatch.Variants.Equals("wave", "waves"));
        Assert.False(WordMatch.Exact.Equals("wave", "waves"));

        // Case never mattered, either way.
        Assert.True(WordMatch.Exact.Equals("Wave", "wave"));
    }

    [Fact]
    public void A_rule_fires_on_a_variant_of_its_word()
    {
        var vocab = new FilenameVocabulary
        {
            Words = new[] { "wave", "surfing" },
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "wave" }),
                        new RuleLine(RuleActions.Order, new[] { "surfing", "wave" })
                    }
                }
            }
        };

        // The ordering is spelled as the rule spells it, not as the file
        // did: the word is lifted out wherever it was found and put back in
        // the rule's own words, so a folder does not end up with
        // surfing-wave and surfing-waves side by side.
        Assert.Equal("surfing-wave-north-shore.mp4",
                     FilenameRules.Apply("waves north shore.mp4", vocab));

        Assert.Equal("surfing-wave-big-day.mp4",
                     FilenameRules.Apply("big waving day.mp4", vocab));
    }

    [Fact]
    public void A_blacklisted_word_takes_its_endings_with_it_when_asked()
    {
        var vocab = new FilenameVocabulary
        {
            Blacklist = new[] { "render" },
            BlacklistMatchMode = WordMatchMode.Variant
        };

        Assert.Equal("beach-day.mp4", FilenameRules.Apply("beach rendered day.mp4", vocab));
        Assert.Equal("beach-day.mp4", FilenameRules.Apply("beach rendering day.mp4", vocab));
    }

    [Fact]
    public void The_blacklist_is_precise_until_told_otherwise()
    {
        // Its own setting, and a stricter default than the word list's.
        // Recognising a word that was not quite meant is a nuisance;
        // deleting one is a word gone from the name.
        var vocab = new FilenameVocabulary { Blacklist = new[] { "render" } };

        Assert.Equal("beach-rendered-day.mp4",
                     FilenameRules.Apply("beach rendered day.mp4", vocab));

        Assert.Equal("beach-day.mp4", FilenameRules.Apply("beach render day.mp4", vocab));
    }

    [Fact]
    public void The_two_lists_do_not_share_a_match_mode()
    {
        // Loose where words are recognised, strict where they are
        // deleted — the pairing the separate settings exist for.
        var vocab = new FilenameVocabulary
        {
            Words = new[] { "wave" },
            Blacklist = new[] { "render" },
            MatchMode = WordMatchMode.Variant,
            BlacklistMatchMode = WordMatchMode.Precise,
            Rules = new[]
            {
                new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, new[] { "wave" }),
                        new RuleLine(RuleActions.Order, new[] { "surf" })
                    }
                }
            }
        };

        // The rule fires on a variant; the blacklisted word does not.
        Assert.Equal("surf-waves-rendered.mp4",
                     FilenameRules.Apply("waves rendered.mp4", vocab));
    }

    [Fact]
    public void Endings_off_means_the_word_as_written()
    {
        var vocab = new FilenameVocabulary
        {
            MatchMode = WordMatchMode.Precise,
            Blacklist = new[] { "render" }
        };

        Assert.Equal("beach-rendered-day.mp4",
                     FilenameRules.Apply("beach rendered day.mp4", vocab));
    }

    [Fact]
    public void A_rule_can_delete_whatever_it_was_looking_for()
    {
        // The list of makes is written once, in the condition. Saying it
        // again in a delete line is the same list twice, and the second
        // copy is the one that falls out of step.
        var vocab = Vocabulary(
            new RuleLine(RuleActions.IfFound, new[] { "ferrari", "ford", "chevrolet" },
                         new[] { "or", "or" }),
            new RuleLine(RuleActions.Order, new[] { "automobile" }),
            new RuleLine(RuleActions.DeleteFound));

        Assert.Equal("automobile-red-1998.mp4",
                     FilenameRules.Apply("ferrari red 1998.mp4", vocab));

        Assert.Equal("automobile-barn-find.mp4",
                     FilenameRules.Apply("barn find chevrolet.mp4", vocab));

        // More than one of them in the same name, and both go.
        Assert.Equal("automobile-vs.mp4",
                     FilenameRules.Apply("ford vs ferrari.mp4", vocab));

        // Nothing it was looking for, so the rule never applies.
        Assert.Equal("red-bicycle.mp4", FilenameRules.Apply("red bicycle.mp4", vocab));
    }

    [Fact]
    public void Deleting_the_found_words_leaves_the_absent_ones_alone()
    {
        // A "NOT found" condition names words that are, by the fact the
        // rule applied at all, not in the name to be deleted — and must
        // not take a word out of the ordering with them.
        var vocab = Vocabulary(
            new RuleLine(RuleActions.IfFound, new[] { "ferrari" }),
            new RuleLine(RuleActions.IfNotFound, new[] { "automobile" }),
            new RuleLine(RuleActions.Order, new[] { "automobile" }),
            new RuleLine(RuleActions.DeleteFound));

        Assert.Equal("automobile-red.mp4", FilenameRules.Apply("ferrari red.mp4", vocab));
    }

    [Fact]
    public void Delete_the_found_words_counts_as_something_to_do()
    {
        // It carries no words of its own, so the usual "a line with no
        // words says nothing" test would have thrown the whole rule out.
        var rule = new RenameRule
        {
            Lines =
            {
                new RuleLine(RuleActions.IfFound, new[] { "ferrari" }),
                new RuleLine(RuleActions.DeleteFound)
            }
        };

        Assert.True(rule.IsUsable);
    }

    [Fact]
    public void A_rule_that_only_asks_a_question_says_nothing()
    {
        var vocab = Vocabulary(new RuleLine(RuleActions.IfFound, new[] { "aggies" }));

        Assert.Equal("aggies-highlights.mp4",
                     FilenameRules.Apply("aggies highlights.mp4", vocab));
    }

    [Fact]
    public void A_rule_saved_in_the_old_shape_still_works()
    {
        // What is in somebody's settings file today: four lists and no
        // lines. The otherwise half becomes its own rule asking the
        // opposite question, which is where it now lives.
        var upgraded = RenameRule.Upgrade(new[]
        {
            new RenameRule
            {
                If = new List<string> { "aggies" },
                Then = new List<string> { "football" },
                Else = new List<string> { "other" }
            }
        });

        Assert.Equal(2, upgraded.Count);
        Assert.Equal(RuleActions.IfFound, upgraded[0].Lines[0].Action);
        Assert.Equal(RuleActions.IfNotFound, upgraded[1].Lines[0].Action);

        var vocab = new FilenameVocabulary { Rules = upgraded };

        Assert.Equal("football-aggies.mp4", FilenameRules.Apply("aggies.mp4", vocab));
        Assert.Equal("other-longhorns.mp4", FilenameRules.Apply("longhorns.mp4", vocab));
    }
}
