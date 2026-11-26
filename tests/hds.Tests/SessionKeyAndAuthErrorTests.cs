using System.Collections;
using hds.accounts;
using hds.auth;

namespace hds.Tests;

// Per-login session keys (margin generates, world uses) and the refused-login AS_AuthReply shape the stock launcher
// can parse. docs/ACCOUNTS.md has the launcher disassembly notes.
public class SessionKeyAndAuthErrorTests
{
    // ---------- AS_AuthReply refusals ----------

    // What launcher.exe 0x443470 reads from every AS_AuthReply body before it checks Status: nine u16 offsets, each
    // either zero (no array) or pointing at [u16 length][bytes] inside the body.
    private static readonly int[] LauncherArrayOffsets = { 0x05, 0x0b, 0x0d, 0x0f, 0x11, 0x13, 0x19, 0x1b, 0x1d };

    [Theory]
    [InlineData(LoginOutcome.BadCredentials, 0x11000002u)]
    [InlineData(LoginOutcome.Banned, 0x11000003u)]
    [InlineData(LoginOutcome.Inactive, 0x11000003u)]
    [InlineData(LoginOutcome.Locked, 0x1100000Fu)]
    [InlineData(LoginOutcome.Unavailable, 0x1100000Fu)]
    public void Refusal_codes_are_ones_the_launcher_shows_a_dialog_for(LoginOutcome outcome, uint status)
    {
        Assert.Equal(status, AuthErrorReply.StatusFor(outcome));
        // Never LTAS_ACCOUNTINACTIVE: the launcher opens InactiveAccountURL in a browser for it.
        Assert.NotEqual(0x11000007u, AuthErrorReply.StatusFor(outcome));
    }

    [Fact]
    public void Refusal_body_parses_the_way_the_launcher_reads_it()
    {
        byte[] body = AuthErrorReply.Body(0x11000002);
        Assert.Equal(0x21, body.Length);
        Assert.True(body.Length >= 0x1d + 2, "every offset the launcher reads is inside the body");
        Assert.Equal(0x0b, body[0]);
        Assert.Equal(0x11000002u, BitConverter.ToUInt32(body, 1));
        foreach (int offset in LauncherArrayOffsets)
            Assert.Equal(0, BitConverter.ToUInt16(body, offset)); // no arrays: nothing to follow or NUL-terminate
    }

    [Fact]
    public void Refusal_is_framed_with_the_one_byte_length_prefix()
    {
        byte[] framed = AuthErrorReply.Framed(0x11000003);
        Assert.Equal(0x21, framed[0]);
        Assert.Equal(AuthErrorReply.Body(0x11000003), framed[1..]);
        Assert.Throws<ArgumentException>(() => AuthErrorReply.Body(0));
        Assert.Throws<ArgumentException>(() => AuthErrorReply.StatusFor(LoginOutcome.Ok));
    }

    // ---------- Session keys ----------

    [Fact]
    public void Every_margin_session_gets_its_own_random_key()
    {
        byte[] first = MarginEncryption.ForNewSession().getTFKey();
        byte[] second = MarginEncryption.ForNewSession().getTFKey();
        Assert.Equal(16, first.Length);
        Assert.NotEqual(first, second);
        Assert.NotEqual(MarginEncryption.LegacyKey, first);
        // The getter hands out a copy: nobody can change a live session's key through it.
        var session = MarginEncryption.ForNewSession();
        byte[] copy = session.getTFKey();
        copy[0] ^= 0xff;
        Assert.NotEqual(copy, session.getTFKey());
    }

    [Fact]
    public void Margin_traffic_needs_the_session_key()
    {
        var server = MarginEncryption.ForNewSession();
        byte[] plain = { 0x04, 0x00, 0x00, 0x00, 0x00 };
        byte[] wire = server.encrypt(plain);

        var client = new MarginEncryption();
        client.SetSessionKey(server.getTFKey());
        byte[] withKey = client.decryptMargin(new byte[] { 0x00 }.Concat(wire).ToArray());
        Assert.Equal(plain, withKey[..plain.Length]);

        // The old fixed key, which every client knew, no longer reads it.
        byte[] withLegacy = new MarginEncryption().decryptMargin(new byte[] { 0x00 }.Concat(wire).ToArray());
        Assert.NotEqual(plain, withLegacy.Length >= plain.Length ? withLegacy[..plain.Length] : withLegacy);
        Assert.Throws<ArgumentException>(() => client.SetSessionKey(new byte[15]));
    }

    [Fact]
    public void World_traffic_round_trips_only_under_the_same_session_key()
    {
        byte[] key = MarginEncryption.ForNewSession().getTFKey();
        byte[] plain = { 0x03, 0x01, 0x02, 0x03, 0x04, 0x05 };
        byte[] wire = new WorldEncryption(key).encrypt(plain, plain.Length, 1, 2, 3);

        ArrayList? same = new WorldEncryption(key).decrypt(wire, wire.Length);
        Assert.NotNull(same);
        Assert.Equal(plain, (byte[])same![0]!);

        Assert.Null(new WorldEncryption().decrypt(wire, wire.Length)); // legacy fixed key: CRC fails, dropped
        Assert.Null(new WorldEncryption(MarginEncryption.ForNewSession().getTFKey()).decrypt(wire, wire.Length));
        Assert.Throws<ArgumentException>(() => new WorldEncryption(new byte[8]));
    }

    [Fact]
    public void Cert_reply_carries_the_session_key_under_the_account_key()
    {
        // MarginClient.certConnectRequest: 00 | key[16] | IV[16], OAEP under the account's modulus, e = 17.
        AccountKey account = AccountKeys.Generate();
        var margin = MarginEncryption.ForNewSession();
        margin.setUserPubKey(new byte[] { 0x00, 0x00, 0x00, 0x11 }, account.Modulus);
        byte[] blob = new byte[33];
        margin.getTFKey().CopyTo(blob, 1);
        margin.getIV().CopyTo(blob, 17);
        byte[] sent = margin.encryptUsersPublic(blob);

        byte[] received = HeadlessClient.AuthLogin.DecryptWithAccountKey(sent, account.Modulus, account.PrivateExponent);
        Assert.Equal(margin.getTFKey(), received[1..17]);
        // Another account's key can't open it.
        AccountKey other = AccountKeys.Generate();
        byte[]? wrong = null;
        try { wrong = HeadlessClient.AuthLogin.DecryptWithAccountKey(sent, other.Modulus, other.PrivateExponent); }
        catch (Exception) { }
        Assert.True(wrong == null || wrong.Length < 17 || !wrong[1..17].SequenceEqual(margin.getTFKey()));
    }
}
