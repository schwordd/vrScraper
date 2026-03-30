using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Services
{
  public class TitleNormalizationService(
    ILogger<TitleNormalizationService> logger,
    IVideoService videoService,
    IServiceProvider serviceProvider) : ITitleNormalizationService
  {
    // ── Unicode Confusables Dictionary ──────────────────────────────────
    // Maps visually similar Unicode characters to their ASCII equivalents.
    // Organized by Unicode block for maintainability.
    private static readonly Dictionary<char, char> Confusables = BuildConfusablesMap();

    private static Dictionary<char, char> BuildConfusablesMap()
    {
      var map = new Dictionary<char, char>();

      // Circled Latin Capital (Ⓐ-Ⓩ) U+24B6-24CF
      for (int i = 0; i < 26; i++)
        map[(char)(0x24B6 + i)] = (char)('A' + i);
      // Circled Latin Small (ⓐ-ⓩ) U+24D0-24E9
      for (int i = 0; i < 26; i++)
        map[(char)(0x24D0 + i)] = (char)('a' + i);

      // Fullwidth Latin Capital (Ａ-Ｚ) U+FF21-FF3A
      for (int i = 0; i < 26; i++)
        map[(char)(0xFF21 + i)] = (char)('A' + i);
      // Fullwidth Latin Small (ａ-ｚ) U+FF41-FF5A
      for (int i = 0; i < 26; i++)
        map[(char)(0xFF41 + i)] = (char)('a' + i);
      // Fullwidth Digits (０-９) U+FF10-FF19
      for (int i = 0; i < 10; i++)
        map[(char)(0xFF10 + i)] = (char)('0' + i);

      // Mathematical Bold Capital (𝐀-𝐙) U+1D400-1D419 — these are surrogate pairs in UTF-16
      // Mathematical Bold Small (𝐚-𝐳) U+1D41A-1D433
      // Mathematical Italic Capital U+1D434-1D44D
      // Mathematical Italic Small U+1D44E-1D467
      // Mathematical Bold Italic Capital U+1D468-1D481
      // Mathematical Bold Italic Small U+1D482-1D49B
      // Mathematical Script Capital U+1D49C-1D4B5
      // Mathematical Script Small U+1D4B6-1D4CF
      // Mathematical Bold Script Capital U+1D4D0-1D4E9
      // Mathematical Bold Script Small U+1D4EA-1D503
      // Mathematical Fraktur Capital U+1D504-1D51D
      // Mathematical Fraktur Small U+1D51E-1D537
      // Mathematical Double-Struck Capital U+1D538-1D551
      // Mathematical Double-Struck Small U+1D552-1D56B
      // Mathematical Bold Fraktur Capital U+1D56C-1D585
      // Mathematical Bold Fraktur Small U+1D586-1D59F
      // Mathematical Sans-Serif Capital U+1D5A0-1D5B9
      // Mathematical Sans-Serif Small U+1D5BA-1D5D3
      // Mathematical Sans-Serif Bold Capital U+1D5D4-1D5ED
      // Mathematical Sans-Serif Bold Small U+1D5EE-1D607
      // Mathematical Sans-Serif Italic Capital U+1D608-1D621
      // Mathematical Sans-Serif Italic Small U+1D622-1D63B
      // Mathematical Sans-Serif Bold Italic Capital U+1D63C-1D655
      // Mathematical Sans-Serif Bold Italic Small U+1D656-1D66F
      // Mathematical Monospace Capital U+1D670-1D689
      // Mathematical Monospace Small U+1D68A-1D6A3
      // Note: These are handled via SurrogatePairConfusables below

      // Small Caps U+1D00+
      map['\u1D00'] = 'A'; // ᴀ
      map['\u0299'] = 'B'; // ʙ
      map['\u1D04'] = 'C'; // ᴄ
      map['\u1D05'] = 'D'; // ᴅ
      map['\u1D07'] = 'E'; // ᴇ
      map['\u0493'] = 'F'; // ғ (Cyrillic, used as F)
      map['\u0262'] = 'G'; // ɢ
      map['\u029C'] = 'H'; // ʜ
      map['\u026A'] = 'I'; // ɪ
      map['\u1D0A'] = 'J'; // ᴊ
      map['\u1D0B'] = 'K'; // ᴋ
      map['\u029F'] = 'L'; // ʟ
      map['\u1D0D'] = 'M'; // ᴍ
      map['\u0274'] = 'N'; // ɴ
      map['\u1D0F'] = 'O'; // ᴏ
      map['\u1D18'] = 'P'; // ᴘ
      // no small cap Q
      map['\u0280'] = 'R'; // ʀ
      map['\u0A51'] = 'S'; // (rare)
      map['\u1D1B'] = 'T'; // ᴛ
      map['\u1D1C'] = 'U'; // ᴜ
      map['\u1D20'] = 'V'; // ᴠ
      map['\u1D21'] = 'W'; // ᴡ
      // no small cap X
      map['\u028F'] = 'Y'; // ʏ
      map['\u1D22'] = 'Z'; // ᴢ

      // Coptic confusables
      map['\u2C80'] = 'A'; // Ⲁ
      map['\u2C81'] = 'a'; // ⲁ
      map['\u2C82'] = 'B'; // Ⲃ
      map['\u2C83'] = 'b'; // ⲃ
      map['\u2C84'] = 'G'; // Ⲅ
      map['\u2C89'] = 'e'; // ⲉ
      map['\u2C8F'] = 'h'; // ⲏ (eta)
      map['\u2C8E'] = 'H'; // Ⲏ
      map['\u2C90'] = 'I'; // Ⲓ
      map['\u2C91'] = 'i'; // ⲓ
      map['\u2C9A'] = 'M'; // Ⲙ
      map['\u2C9B'] = 'm'; // ⲙ (not exact but used)
      map['\u2C9C'] = 'N'; // Ⲛ
      map['\u2C9D'] = 'n'; // ⲛ
      map['\u2C9E'] = 'X'; // Ⲝ
      map['\u2CA0'] = 'O'; // Ⲟ
      map['\u2CA1'] = 'o'; // ⲟ
      map['\u2CA2'] = 'P'; // Ⲡ
      map['\u2CA3'] = 'p'; // ⲡ
      map['\u2CA4'] = 'R'; // Ⲥ (actually Coptic Sima, used as R-like)
      map['\u2CA5'] = 'r'; // ⲥ
      map['\u2CA6'] = 'T'; // Ⲧ
      map['\u2CA7'] = 't'; // ⲧ
      map['\u2CA8'] = 'U'; // Ⲩ
      map['\u2CA9'] = 'u'; // ⲩ
      map['\u2CAA'] = 'F'; // Ⲫ
      map['\u2CAB'] = 'f'; // ⲫ
      map['\u2CAC'] = 'K'; // Ⲭ
      map['\u2CAD'] = 'k'; // ⲭ
      map['\u2CB0'] = 'W'; // Ⲱ
      map['\u2CB1'] = 'w'; // ⲱ
      // Coptic used as 's' in obfuscated titles
      map['\u0376'] = 'S'; // Ϩ → S (seen in DB as Scarlett)

      // Old Italic (𐌵 seen in DB)
      // These are surrogate pairs, handled below

      // Canadian Aboriginal Syllabics
      map['\u15E9'] = 'A'; // ᗩ
      map['\u144E'] = 'N'; // ᑎ
      map['\u14AA'] = 'L'; // ᒪ
      map['\u15EA'] = 'D'; // ᗪ
      map['\u15F4'] = 'W'; // ᗴ→W? Actually ᗯ
      map['\u15EF'] = 'W'; // ᗯ
      map['\u1455'] = 'J'; // ᑕ→C? Actually ᒍ
      map['\u1455'] = 'C'; // ᑕ
      map['\u1466'] = 'S'; // ᑦ? ᔕ
      map['\u1585'] = 'Q'; // ᖅ
      map['\u1587'] = 'R'; // ᖇ
      map['\u1591'] = 'F'; // ᖑ? ᑫ→Q
      map['\u15B4'] = 'V'; // ᖴ
      map['\u14F0'] = 'Y'; // ᓰ→? ᓎ
      map['\u1587'] = 'R'; // ᖇ
      // More precise mappings from actual DB data
      map['\u1597'] = 'K'; // ᖗ
      map['\u14B2'] = 'G'; // ᒲ→G? ᘜ
      map['\u15B2'] = 'b'; // ᖲ
      map['\u1490'] = 'B'; // ᒐ→B? ᗷ
      map['\u15F7'] = 'B'; // ᗷ
      map['\u1D18'] = 'P'; // ᴘ (small cap)
      map['\u1D35'] = 'I'; // ᴵ (modifier)
      map['\u1466'] = 'S'; // ᑦ
      map['\u1405'] = 'E'; // ᐅ→? ᗴ
      map['\u15B4'] = 'V'; // ᖴ
      map['\u1D2F'] = 'V'; // ᐯ
      map['\u15AF'] = 'V'; // ᖯ→V?
      // ᐯ is actually U+142F
      map['\u142F'] = 'V'; // ᐯ
      map['\u1466'] = 'S'; // ᑦ
      map['\u1515'] = 'P'; // ᔕ→S
      map['\u1515'] = 'S'; // ᔕ
      map['\u1D18'] = 'P'; // ᴘ
      map['\u1450'] = 'T'; // ᑐ→T
      map['\u1496'] = 'D'; // ᒖ→D?
      map['\u148C'] = 'Z'; // ᒌ→Z? ᘔ
      map['\u161A'] = 'Z'; // ᘚ→Z? ᘔ
      map['\u1614'] = 'Z'; // ᘔ
      map['\u14BA'] = 'M'; // ᒺ→M? ᗰ
      map['\u15F0'] = 'M'; // ᗰ

      // Armenian/Cyrillic confusables
      map['\u027E'] = 'r'; // ɾ
      map['\u0585'] = 'o'; // օ
      map['\u0282'] = 's'; // ʂ
      map['\u04BD'] = 'e'; // ҽ
      map['\u0561'] = 'a'; // ա→a
      map['\u0567'] = 'e'; // է→e (Armenian Ech)
      map['\u056B'] = 'i'; // ի
      map['\u0578'] = 'n'; // ո→n
      map['\u057D'] = 's'; // ս→s (Armenian Now)
      map['\u0584'] = 'q'; // ք→q
      map['\u0566'] = 'g'; // զ→g?
      map['\u0581'] = 'c'; // ց→c
      map['\u0571'] = 'd'; // ձ→d
      map['\u0575'] = 'y'; // յ→y
      map['\u0574'] = 'm'; // մ→m
      map['\u0570'] = 'h'; // հ→h
      map['\u0569'] = 't'; // թ→t

      // Cyrillic confusables
      map['\u0430'] = 'a'; // а
      map['\u0435'] = 'e'; // е
      map['\u043E'] = 'o'; // о
      map['\u0440'] = 'p'; // р→p
      map['\u0441'] = 'c'; // с
      map['\u0443'] = 'y'; // у→y
      map['\u0445'] = 'x'; // х→x
      map['\u0410'] = 'A'; // А
      map['\u0412'] = 'B'; // В→B
      map['\u0415'] = 'E'; // Е
      map['\u041A'] = 'K'; // К
      map['\u041C'] = 'M'; // М
      map['\u041D'] = 'H'; // Н→H
      map['\u041E'] = 'O'; // О
      map['\u0420'] = 'P'; // Р
      map['\u0421'] = 'C'; // С
      map['\u0422'] = 'T'; // Т
      map['\u0425'] = 'X'; // Х

      // CJK look-alikes (from actual DB data)
      map['\u4E47'] = 'E'; // 乇
      map['\u4E02'] = 'S'; // 丂
      map['\u3112'] = 'T'; // ㄒ
      map['\u5C3A'] = 'R'; // 尺
      map['\u51E0'] = 'C'; // 几→N
      map['\u51E0'] = 'N'; // 几
      map['\u531A'] = 'C'; // 匚
      map['\u5369'] = 'P'; // 卩
      map['\u4E28'] = 'I'; // 丨
      map['\u5E72'] = 'H'; // 干→H?
      // More CJK from DB
      map['\u30E2'] = 'E'; // モ→? not quite

      // Upside-down characters (individual char mapping — reversal handled in NormalizeUnicode)
      map['\u01DD'] = 'e'; // ǝ
      map['\u0279'] = 'r'; // ɹ
      map['\u0131'] = 'i'; // ı (dotless i)
      map['\u0250'] = 'a'; // ɐ
      map['\u0254'] = 'c'; // ɔ (open o, used as c)
      map['\u025F'] = 'f'; // ɟ (looks like f upside down, used as f)
      map['\u0265'] = 'h'; // ɥ
      map['\u026F'] = 'm'; // ɯ
      map['\u0270'] = 'w'; // ɰ
      map['\u0287'] = 't'; // ʇ
      map['\u028C'] = 'v'; // ʌ
      map['\u028D'] = 'w'; // ʍ
      map['\u028E'] = 'y'; // ʎ
      map['\u0285'] = 'l'; // ʅ (squat reversed esh, used as l)
      map['\u029E'] = 'k'; // ʞ
      map['\u0253'] = 'g'; // ɓ (upside-down g — visually looks like rotated g)
      map['\u1D09'] = 'i'; // ᴉ (turned i)
      map['\u0183'] = 'b'; // ƃ
      map['\uA72D'] = 'd'; // ꜭ (turned D variant)
      map['\uA4F7'] = 'd'; // ꓷ (turned D — actual codepoint in DB data)
      map['\uA7B0'] = 'K'; // Ꝁ
      map['\uA4D8'] = 'K'; // ꓘ (turned K)
      map['\uA4E9'] = 'Z'; // ꓩ (turned Z)
      map['\u0222'] = 'S'; // Ȣ→S? (Շ is Armenian)
      map['\u0547'] = '2'; // Շ (Armenian Sha, used as flipped 2)

      // Currency/Stroke letter confusables (from actual DB data)
      map['\u20B3'] = 'A'; // ₳
      map['\u0244'] = 'U'; // Ʉ
      map['\u20B4'] = 'S'; // ₴
      map['\u20AE'] = 'T'; // ₮
      map['\u20B1'] = 'P'; // ₱
      map['\u20B5'] = 'C'; // ₵
      map['\u20A3'] = 'F'; // ₣
      map['\u20A5'] = 'M'; // ₥
      map['\u2C67'] = 'H'; // Ⱨ
      map['\u2C68'] = 'h'; // ⱨ
      map['\u024E'] = 'Y'; // Ɏ
      map['\u2C60'] = 'L'; // Ⱡ
      map['\u2C61'] = 'l'; // ⱡ
      map['\u0110'] = 'D'; // Đ
      map['\u0111'] = 'd'; // đ
      map['\u0246'] = 'E'; // Ɇ
      map['\u0247'] = 'e'; // ɇ
      map['\u024C'] = 'R'; // Ɽ
      map['\u024D'] = 'r'; // ɽ
      map['\u2C64'] = 'R'; // Ɽ (alternate)
      map['\u019E'] = 'n'; // ƞ
      map['\u2C66'] = 't'; // ⱦ
      map['\u0142'] = 'l'; // ł
      map['\u0141'] = 'L'; // Ł
      map['\u00D8'] = 'O'; // Ø
      map['\u00F8'] = 'o'; // ø
      map['\u0E3F'] = 'B'; // ฿ (Thai Baht, used as B)
      map['\u20B2'] = 'G'; // ₲
      map['\u20A0'] = 'E'; // ₠→CE? just E
      map['\u20A7'] = 'P'; // ₧ (Peseta)

      // Latin Extended-B confusables
      map['\u0189'] = 'D'; // Ɖ (African D)
      map['\u018E'] = 'E'; // Ǝ (reversed E)
      map['\u018F'] = 'E'; // Ə (Schwa, used as E)
      map['\u0186'] = 'C'; // Ɔ (Open O, used as C)
      map['\u0190'] = 'E'; // Ɛ (Open E)
      map['\u0191'] = 'F'; // Ƒ
      map['\u0193'] = 'G'; // Ɠ
      map['\u0197'] = 'I'; // Ɨ
      map['\u019C'] = 'M'; // Ɯ (turned M)
      map['\u019D'] = 'N'; // Ɲ
      map['\u01A4'] = 'P'; // Ƥ
      map['\u01AC'] = 'T'; // Ƭ
      map['\u01B2'] = 'V'; // Ʋ
      map['\u0224'] = 'Z'; // Ȥ
      map['\u0187'] = 'C'; // Ƈ
      map['\u0198'] = 'K'; // Ƙ
      map['\u01A0'] = 'O'; // Ơ
      map['\u01AF'] = 'U'; // Ư
      map['\u018C'] = 'd'; // ƌ (d with topbar)
      map['\u0192'] = 'f'; // ƒ (f with hook)
      map['\u025D'] = 'e'; // ɝ (reversed open e)
      map['\u0277'] = 'w'; // ɷ (closed omega, used as w)

      // Hebrew used as Latin look-alikes
      map['\u05E0'] = 'j'; // נ (nun → visually used as j in obfuscated text)
      map['\u05D5'] = 'u'; // ו (vav)

      // More Greek/Coptic confusables
      map['\u03C9'] = 'w'; // ω (omega)
      map['\u03B6'] = 'z'; // ζ (zeta)
      map['\u03C4'] = 't'; // τ (tau)
      map['\u03BD'] = 'v'; // ν (nu → v)
      map['\u03BA'] = 'k'; // κ (kappa)
      map['\u03C1'] = 'p'; // ρ (rho → p)
      map['\u03B7'] = 'n'; // η (eta → n)
      map['\u03C6'] = 'f'; // φ (phi → f)

      // Latin Extended Additional
      map['\u0219'] = 's'; // ș (s with comma below)
      map['\u021B'] = 't'; // ț (t with comma below)
      map['\u1E63'] = 's'; // ṣ
      map['\u1E6D'] = 't'; // ṭ

      // CJK used as Latin look-alikes (more from DB)
      map['\u3007'] = 'O'; // 〇 (ideographic zero → O)
      map['\u5200'] = 'D'; // 刀 (knife → D)
      map['\u4E39'] = 'A'; // 丹 → A
      map['\uAAB6'] = 'l'; // ꪶ (Tai Viet → l)

      // Combining/modifier letters that should just be stripped
      // (handled by the "strip unknown" logic)

      // Armenian additional
      map['\u054A'] = 'P'; // Պ → P

      // More Cyrillic confusables (used in mixed-script obfuscation)
      map['\u0454'] = 'e'; // є (Cyrillic ie)
      map['\u0455'] = 's'; // ѕ (Cyrillic dze)
      map['\u0432'] = 'b'; // в (Cyrillic ve → used as b visually)
      map['\u0456'] = 'i'; // і (Cyrillic i)
      map['\u0457'] = 'i'; // ї (Cyrillic yi)
      map['\u0442'] = 't'; // т (Cyrillic te)
      map['\u043D'] = 'n'; // н (Cyrillic en)
      map['\u0448'] = 'w'; // ш (Cyrillic sha → w)
      map['\u043C'] = 'm'; // м (Cyrillic em)
      map['\u043A'] = 'k'; // к (Cyrillic ka)
      map['\u0440'] = 'r'; // р (Cyrillic er → was mapped to p, but visually used as r too)

      // Cent/currency signs used as letters
      map['\u00A2'] = 'c'; // ¢ → c

      // Latin Extended-A common diacritics (FormKD fails under InvariantGlobalization)
      map['\u010E'] = 'D'; // Ď
      map['\u010F'] = 'd'; // ď
      map['\u0164'] = 'T'; // Ť
      map['\u0165'] = 't'; // ť
      map['\u0174'] = 'W'; // Ŵ
      map['\u0175'] = 'w'; // ŵ
      map['\u0159'] = 'r'; // ř
      map['\u0158'] = 'R'; // Ř
      map['\u0155'] = 'r'; // ŕ
      map['\u0154'] = 'R'; // Ŕ
      map['\u0117'] = 'e'; // ė
      map['\u0116'] = 'E'; // Ė
      map['\u015F'] = 's'; // ş
      map['\u015E'] = 'S'; // Ş
      map['\u011F'] = 'g'; // ğ
      map['\u011E'] = 'G'; // Ğ
      map['\u0148'] = 'n'; // ň
      map['\u0147'] = 'N'; // Ň
      map['\u013E'] = 'l'; // ľ
      map['\u013D'] = 'L'; // Ľ
      map['\u017E'] = 'z'; // ž
      map['\u017D'] = 'Z'; // Ž
      map['\u0161'] = 's'; // š
      map['\u0160'] = 'S'; // Š
      map['\u010D'] = 'c'; // č
      map['\u010C'] = 'C'; // Č
      map['\u0144'] = 'n'; // ń
      map['\u0143'] = 'N'; // Ń
      map['\u024E'] = 'Y'; // Ɏ
      map['\u024F'] = 'y'; // ɏ
      map['\u0186'] = 'C'; // Ɔ (Open O → used as C)
      map['\u01B2'] = 'V'; // Ʋ
      map['\u0166'] = 'T'; // Ŧ
      map['\u0167'] = 't'; // ŧ
      map['\u0126'] = 'H'; // Ħ
      map['\u20A6'] = 'N'; // ₦ (Naira → N)

      // Greek confusables
      map['\u0391'] = 'A'; // Α
      map['\u0392'] = 'B'; // Β
      map['\u0395'] = 'E'; // Ε
      map['\u0396'] = 'Z'; // Ζ
      map['\u0397'] = 'H'; // Η
      map['\u0399'] = 'I'; // Ι
      map['\u039A'] = 'K'; // Κ
      map['\u039C'] = 'M'; // Μ
      map['\u039D'] = 'N'; // Ν
      map['\u039F'] = 'O'; // Ο
      map['\u03A1'] = 'P'; // Ρ
      map['\u03A4'] = 'T'; // Τ
      map['\u03A5'] = 'Y'; // Υ
      map['\u03A7'] = 'X'; // Χ
      map['\u03B1'] = 'a'; // α
      map['\u03B5'] = 'e'; // ε
      map['\u03B9'] = 'i'; // ι
      map['\u03BF'] = 'o'; // ο
      map['\u03C5'] = 'u'; // υ
      map['\u03C3'] = 's'; // σ (sigma)
      map['\u03B2'] = 'b'; // β (used as b)
      map['\u03B4'] = 'd'; // δ (used as d)
      map['\u03B3'] = 'y'; // γ (used as y)

      // Modifier letters / Superscripts
      map['\u1D43'] = 'a'; // ᵃ
      map['\u1D47'] = 'b'; // ᵇ
      map['\u1D9C'] = 'c'; // ᶜ
      map['\u1D48'] = 'd'; // ᵈ
      map['\u1D49'] = 'e'; // ᵉ
      map['\u1DA0'] = 'f'; // ᶠ
      map['\u1D4D'] = 'g'; // ᵍ
      map['\u02B0'] = 'h'; // ʰ
      map['\u2071'] = 'i'; // ⁱ
      map['\u02B2'] = 'j'; // ʲ
      map['\u1D4F'] = 'k'; // ᵏ
      map['\u02E1'] = 'l'; // ˡ
      map['\u1D50'] = 'm'; // ᵐ
      map['\u207F'] = 'n'; // ⁿ
      map['\u1D52'] = 'o'; // ᵒ
      map['\u1D56'] = 'p'; // ᵖ
      map['\u02B3'] = 'r'; // ʳ
      map['\u02E2'] = 's'; // ˢ
      map['\u1D57'] = 't'; // ᵗ
      map['\u1D58'] = 'u'; // ᵘ
      map['\u1D5B'] = 'v'; // ᵛ
      map['\u02B7'] = 'w'; // ʷ
      map['\u02E3'] = 'x'; // ˣ
      map['\u02B8'] = 'y'; // ʸ

      // Subscript digits
      map['\u2080'] = '0'; // ₀
      map['\u2081'] = '1'; // ₁
      map['\u2082'] = '2'; // ₂
      map['\u2083'] = '3'; // ₃
      map['\u2084'] = '4'; // ₄
      map['\u2085'] = '5'; // ₅
      map['\u2086'] = '6'; // ₆
      map['\u2087'] = '7'; // ₇
      map['\u2088'] = '8'; // ₈
      map['\u2089'] = '9'; // ₉

      // Superscript digits
      map['\u2070'] = '0'; // ⁰
      map['\u00B9'] = '1'; // ¹
      map['\u00B2'] = '2'; // ²
      map['\u00B3'] = '3'; // ³
      map['\u2074'] = '4'; // ⁴
      map['\u2075'] = '5'; // ⁵
      map['\u2076'] = '6'; // ⁶
      map['\u2077'] = '7'; // ⁷
      map['\u2078'] = '8'; // ⁸
      map['\u2079'] = '9'; // ⁹

      // Latin Extended-B / IPA that are used as look-alikes
      map['\u0127'] = 'h'; // ħ (used as h)
      map['\u026E'] = 'l'; // ɮ→l?
      map['\u0268'] = 'i'; // ɨ→i
      map['\u0289'] = 'u'; // ʉ→u
      map['\u1E9E'] = 'S'; // ẞ→S
      map['\uA7B5'] = 'r'; // ꞵ→? ꞅ
      map['\uA784'] = 'R'; // Ꞅ→R (Latin capital letter insular R)
      map['\uA785'] = 'r'; // ꞅ→r (Latin small letter insular r)

      // Letterlike symbols (includes BMP chars for Script/Fraktur/DoubleStruck gaps)
      map['\u2102'] = 'C'; // ℂ (Double-Struck C)
      map['\u210A'] = 'g'; // ℊ (Script small g)
      map['\u210B'] = 'H'; // ℋ (Script Capital H)
      map['\u210C'] = 'H'; // ℌ (Fraktur Capital H)
      map['\u210D'] = 'H'; // ℍ (Double-Struck H)
      map['\u210E'] = 'h'; // ℎ (Italic small h / Planck constant)
      map['\u2110'] = 'I'; // ℐ (Script Capital I)
      map['\u2111'] = 'I'; // ℑ (Fraktur Capital I)
      map['\u2112'] = 'L'; // ℒ (Script Capital L)
      map['\u2113'] = 'l'; // ℓ
      map['\u2115'] = 'N'; // ℕ (Double-Struck N)
      map['\u2119'] = 'P'; // ℙ (Double-Struck P)
      map['\u211A'] = 'Q'; // ℚ (Double-Struck Q)
      map['\u211B'] = 'R'; // ℛ (Script Capital R)
      map['\u211C'] = 'R'; // ℜ (Fraktur Capital R)
      map['\u211D'] = 'R'; // ℝ (Double-Struck R)
      map['\u2124'] = 'Z'; // ℤ (Double-Struck Z)
      map['\u2128'] = 'Z'; // ℨ (Fraktur Capital Z)
      map['\u212C'] = 'B'; // ℬ (Script Capital B)
      map['\u212D'] = 'C'; // ℭ (Fraktur Capital C)
      map['\u212F'] = 'e'; // ℯ (Script small e)
      map['\u2130'] = 'E'; // ℰ (Script Capital E)
      map['\u2131'] = 'F'; // ℱ (Script Capital F)
      map['\u2133'] = 'M'; // ℳ (Script Capital M)
      map['\u2134'] = 'o'; // ℴ (Script small o)
      map['\u2139'] = 'i'; // ℹ

      // Special: € used as 'e' in DB
      map['\u20AC'] = 'e'; // €

      // Misc seen in DB
      map['\u03DC'] = 'F'; // Ϝ (Greek digamma)
      map['\u03DD'] = 'f'; // ϝ
      map['\u03F2'] = 'c'; // ϲ (Greek lunate sigma)
      map['\u03F9'] = 'C'; // Ϲ
      map['\u03FB'] = 'M'; // ϻ→M (seen in DB)

      // ── Lisu Script (U+A4D0-A4FF) ────────────────────────────────
      map['\uA4D0'] = 'A'; // ꓐ
      map['\uA4D1'] = 'N'; // ꓑ
      map['\uA4D5'] = 'T'; // ꓕ
      map['\uA4D6'] = 'G'; // ꓖ
      map['\uA4DA'] = 'C'; // ꓚ
      map['\uA4DB'] = 'C'; // ꓛ
      map['\uA4DD'] = 'F'; // ꓝ
      map['\uA4DE'] = 'Y'; // ꓞ
      map['\uA4E0'] = 'N'; // ꓠ
      map['\uA4E2'] = 'S'; // ꓢ
      map['\uA4E3'] = 'N'; // ꓣ
      map['\uA4E4'] = 'U'; // ꓤ
      map['\uA4E6'] = 'V'; // ꓦ
      map['\uA4E7'] = 'H'; // ꓧ
      map['\uA4E8'] = 'Y'; // ꓨ
      map['\uA4EC'] = 'X'; // ꓬ
      map['\uA4ED'] = 'G'; // ꓭ
      map['\uA4EE'] = 'E'; // ꓮ
      map['\uA4F0'] = 'A'; // ꓰ
      map['\uA4F2'] = 'I'; // ꓲ
      map['\uA4F5'] = 'F'; // ꓵ
      map['\uA4F6'] = 'F'; // ꓶ

      // ── Cherokee (U+13A0-13FF) ───────────────────────────────────
      map['\u13A5'] = 'I'; // Ꭵ
      map['\u13A6'] = 'T'; // Ꮦ (approximation)
      map['\u13A7'] = 'A'; // Ꮧ (approximation)
      map['\u13AA'] = 'L'; // Ꮮ
      map['\u13AB'] = 'E'; // Ꮛ (approximation)
      map['\u13AD'] = 'L'; // Ꮭ
      map['\u13B1'] = 'N'; // Ꮑ
      map['\u13B2'] = 'H'; // Ꮒ
      map['\u13BE'] = 'P'; // Ꭾ
      map['\u13C6'] = 'G'; // Ꮆ
      map['\u13C7'] = 'M'; // Ꮇ
      map['\u13D2'] = 'S'; // Ꮪ
      map['\u13DC'] = 'U'; // Ꮜ
      map['\u13E2'] = 'R'; // Ꮢ
      map['\u13F9'] = 'W'; // Ꮹ

      // ── Additional IPA/Latin/CJK ─────────────────────────────────
      map['\u0258'] = 'e'; // ɘ (reversed e)
      map['\u0252'] = 'a'; // ɒ (turned alpha)
      map['\u027F'] = 'r'; // ɿ (reversed r with fishhook)
      map['\u01A8'] = 's'; // ƨ (tone two)
      map['\u157C'] = 'H'; // ᕼ (Canadian Syllabics H)
      map['\u2C6F'] = 'A'; // Ɐ (turned A)
      map['\u5344'] = 'A'; // 卄→A (CJK)
      map['\u3116'] = 'O'; // ㄖ (Bopomofo)
      map['\u3125'] = 'L'; // ㄥ (Bopomofo)
      map['\u5343'] = 'F'; // 千 (CJK)
      map['\u4E59'] = 'Z'; // 乙 (CJK)
      map['\u4E05'] = 'T'; // 丅 (CJK)
      map['\u3129'] = 'U'; // ㄩ (Bopomofo)
      map['\u5C71'] = 'W'; // 山 (CJK)

      // ── Circled Digits ①-⑳ ──────────────────────────────────────
      for (int i = 0; i < 20; i++)
        map[(char)(0x2460 + i)] = i < 9 ? (char)('1' + i) : ' '; // ①-⑨ → 1-9, rest → space
      // Negative circled ❶-❿
      for (int i = 0; i < 10; i++)
        map[(char)(0x2776 + i)] = (char)('1' + i);

      return map;
    }

    // Surrogate pair mappings for characters above U+FFFF (Mathematical Alphanumeric Symbols)
    private static readonly Dictionary<int, char> SurrogatePairConfusables = BuildSurrogatePairMap();

    private static Dictionary<int, char> BuildSurrogatePairMap()
    {
      var map = new Dictionary<int, char>();

      // Mathematical Alphanumeric Symbols U+1D400-1D7FF
      // Each block has 26 uppercase + 26 lowercase (some with gaps for reserved chars)
      int[][] ranges = [
        // [startCodepoint, 'A' or 'a' base]
        [0x1D400, 'A'], [0x1D41A, 'a'], // Bold
        [0x1D434, 'A'], [0x1D44E, 'a'], // Italic
        [0x1D468, 'A'], [0x1D482, 'a'], // Bold Italic
        [0x1D49C, 'A'], [0x1D4B6, 'a'], // Script
        [0x1D4D0, 'A'], [0x1D4EA, 'a'], // Bold Script
        [0x1D504, 'A'], [0x1D51E, 'a'], // Fraktur
        [0x1D538, 'A'], [0x1D552, 'a'], // Double-Struck
        [0x1D56C, 'A'], [0x1D586, 'a'], // Bold Fraktur
        [0x1D5A0, 'A'], [0x1D5BA, 'a'], // Sans-Serif
        [0x1D5D4, 'A'], [0x1D5EE, 'a'], // Sans-Serif Bold
        [0x1D608, 'A'], [0x1D622, 'a'], // Sans-Serif Italic
        [0x1D63C, 'A'], [0x1D656, 'a'], // Sans-Serif Bold Italic
        [0x1D670, 'A'], [0x1D68A, 'a'], // Monospace
      ];

      foreach (var range in ranges)
      {
        int start = range[0];
        char baseChar = (char)range[1];
        for (int i = 0; i < 26; i++)
        {
          int cp = start + i;
          map[cp] = (char)(baseChar + i);
        }
      }

      // Mathematical Bold Digits U+1D7CE-1D7D7
      for (int i = 0; i < 10; i++)
        map[0x1D7CE + i] = (char)('0' + i);
      // Double-Struck Digits U+1D7D8-1D7E1
      for (int i = 0; i < 10; i++)
        map[0x1D7D8 + i] = (char)('0' + i);
      // Sans-Serif Digits U+1D7E2-1D7EB
      for (int i = 0; i < 10; i++)
        map[0x1D7E2 + i] = (char)('0' + i);
      // Sans-Serif Bold Digits U+1D7EC-1D7F5
      for (int i = 0; i < 10; i++)
        map[0x1D7EC + i] = (char)('0' + i);
      // Monospace Digits U+1D7F6-1D7FF
      for (int i = 0; i < 10; i++)
        map[0x1D7F6 + i] = (char)('0' + i);

      // Old Italic 𐌵 U+10335 → u (seen in DB for "pussy")
      map[0x10335] = 'u';

      // Enclosed Alphanumeric Supplement (emoji-style letters)
      // Squared Latin Capital Letters: 🄰-🅉 U+1F130-1F149
      for (int i = 0; i < 26; i++)
        map[0x1F130 + i] = (char)('A' + i);
      // Negative Circled Latin Capital Letters: 🅐-🅩 U+1F150-1F169
      for (int i = 0; i < 26; i++)
        map[0x1F150 + i] = (char)('A' + i);
      // Negative Squared Latin Capital Letters: 🅰-🆉 U+1F170-1F189
      for (int i = 0; i < 26; i++)
        map[0x1F170 + i] = (char)('A' + i);
      // Regional Indicator Symbols: 🇦-🇿 U+1F1E6-1F1FF (used in flag emoji but also as letters)
      for (int i = 0; i < 26; i++)
        map[0x1F1E6 + i] = (char)('A' + i);

      return map;
    }

    // ── Accent Map for direct diacritic stripping ──────────────────────
    private static readonly Dictionary<char, char> AccentMap = BuildAccentMap();
    private static Dictionary<char, char> BuildAccentMap()
    {
      var map = new Dictionary<char, char>();
      void Add(char baseChar, string accented)
      {
        foreach (var c in accented)
        {
          map[c] = baseChar;
          map[char.ToUpperInvariant(c)] = char.ToUpperInvariant(baseChar);
        }
      }
      Add('a', "àáâãäåăạǎǟ"); Add('e', "èéêëěẽęệėẻ"); Add('i', "ìíîïĩịĭǐ");
      Add('o', "òóôõöőọǒȍ"); Add('u', "ùúûüůũụǔ"); Add('y', "ýÿŷ"); Add('n', "ñňń");
      Add('c', "çčć"); Add('s', "šśşŝ"); Add('z', "žźżẑ"); Add('r', "řŕ");
      Add('d', "ďđ"); Add('t', "ťŧ"); Add('l', "ľłĺ"); Add('g', "ğǧ");
      Add('h', "ħȟ"); Add('b', "ƀ"); Add('w', "ŵ");
      return map;
    }

    // ── Leet-Speak Mappings ────────────────────────────────────────────
    private static readonly Dictionary<char, char> LeetMap = new()
    {
      ['@'] = 'a',
      ['$'] = 's',
      ['0'] = 'o',
      ['3'] = 'e',
      ['1'] = 'l',
      ['4'] = 'a',
      ['5'] = 's',
      ['6'] = 'b',
      ['7'] = 't',
      ['!'] = 'i',
      ['9'] = 'g',
    };

    // ── Ambiguous character pairs for dictionary-based post-correction ──
    private static readonly (char from, char to)[] AmbiguousPairs =
    [
      ('l', 'i'), ('i', 'l'),  // l ↔ i
      ('I', 'l'), ('v', 'u'),  // I → l, v → u
      ('u', 'v'),              // u → v
      ('b', 'g'), ('g', 'b'),  // b ↔ g (upside-down ambiguity)
    ];

    // ── Dictionary for post-processing correction ──
    private static readonly HashSet<string> WordDictionary = BuildDictionary();
    private readonly HashSet<string> _dynamicDictionary = new(StringComparer.OrdinalIgnoreCase);
    private bool _dynamicDictionaryLoaded;

    private static HashSet<string> BuildDictionary()
    {
      var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      // Load embedded english_words.txt (234k NLTK words)
      var assembly = System.Reflection.Assembly.GetExecutingAssembly();
      using var stream = assembly.GetManifestResourceStream("vrScraper.Resources.english_words.txt");
      if (stream != null)
      {
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
          line = line.Trim();
          if (line.Length >= 3)
            words.Add(line);
        }
      }

      // Add domain-specific words not in NLTK
      foreach (var w in "milf gilf dilf cougar pawg bbc bwc bbw ssbbw pov vr threesome foursome fivesome gangbang bukkake orgy creampie facial cumshot blowjob handjob footjob rimjob titjob assjob throatpie deepthroat gagging edging orgasm squirting fisting pegging strapon dildo vibrator fleshlight gaping prolapse bondage bdsm shibari femdom maledom cosplay roleplay lingerie stockings fishnets pantyhose bodysuit corset bikini stepmom stepdad stepsister stepbrother stepdaughter stepson babysitter nanny roommate coworker landlord pornstar webcam livestream onlyfans chaturbate brazzers naughtyamerica realitykings bangbros mofos tushy vixen blacked wankz virtualreal darkroom slr vrbangers vrhush vrallure vrlatina hentai ahegao waifu senpai ecchi harem softcore hardcore interracial cuckold hotwife bdsm gangbanged creampied deepthroated squirted fisted pegged rimmed cuckolded dominated submitted".Split(' '))
        words.Add(w);

      return words;
    }

    // ── Canonical index for O(1) ambiguity resolution (i↔l, v↔u, b↔g) ──
    private static readonly Dictionary<string, List<string>> CanonicalIndex = BuildCanonicalIndex();

    private static Dictionary<string, List<string>> BuildCanonicalIndex()
    {
      var index = new Dictionary<string, List<string>>();
      foreach (var word in WordDictionary)
      {
        var canon = Canonicalize(word);
        if (!index.ContainsKey(canon))
          index[canon] = [];
        index[canon].Add(word);
      }
      return index;
    }

    private static string Canonicalize(string word)
    {
      var sb = new StringBuilder(word.Length);
      foreach (var c in word.ToLowerInvariant())
      {
        sb.Append(c switch
        {
          'i' or 'l' => '*',
          'v' or 'u' => '#',
          'b' or 'g' => '@',
          _ => c
        });
      }
      return sb.ToString();
    }

    private void EnsureDynamicDictionary()
    {
      if (_dynamicDictionaryLoaded) return;
      _dynamicDictionaryLoaded = true;

      try
      {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

        // Add all star name parts
        foreach (var star in context.Stars.Select(s => s.Name).ToList())
        {
          foreach (var part in star.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            _dynamicDictionary.Add(part.ToLowerInvariant());
        }

        // Add all tag names
        foreach (var tag in context.Tags.Select(t => t.Name).ToList())
        {
          _dynamicDictionary.Add(tag.ToLowerInvariant());
          foreach (var part in tag.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            _dynamicDictionary.Add(part.ToLowerInvariant());
        }

        // Extract vocabulary from non-obfuscated titles
        // Only add words that are ALREADY in the main dictionary (prevents pollution from
        // obfuscated titles that IsObfuscated() missed, like i/l swaps: "Mldnlght")
        var cleanTitles = context.VideoItems
          .Select(v => v.Title)
          .ToList()
          .Where(t => !IsObfuscated(t))
          .ToList();

        int cleanWordsAdded = 0;
        foreach (var title in cleanTitles)
        {
          foreach (var word in title.Split([' ', '-', ',', '.', '(', ')', ':', '\'', '"', '!', '?'], StringSplitOptions.RemoveEmptyEntries))
          {
            var clean = word.Trim().ToLowerInvariant();
            if (clean.Length >= 3 && clean.All(char.IsLetter))
            {
              // Only add if it's a known English word (prevents "mldnlght" etc.)
              if (WordDictionary.Contains(clean))
              {
                _dynamicDictionary.Add(clean);
                cleanWordsAdded++;
              }
            }
          }
        }

        logger.LogInformation("Dynamic dictionary loaded: {Count} words (from stars, tags, and {Titles} clean titles)",
          _dynamicDictionary.Count, cleanTitles.Count);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Failed to load dynamic dictionary");
      }
    }

    private bool IsInDictionary(string word)
    {
      var lower = word.ToLowerInvariant();
      if (WordDictionary.Contains(lower) || _dynamicDictionary.Contains(lower))
        return true;
      // Check common inflections: strip trailing s/es/ed/ing/er
      if (lower.EndsWith("s") && WordDictionary.Contains(lower[..^1]))
        return true;
      if (lower.EndsWith("es") && WordDictionary.Contains(lower[..^2]))
        return true;
      if (lower.EndsWith("ed") && WordDictionary.Contains(lower[..^2]))
        return true;
      if (lower.EndsWith("ing") && lower.Length > 5 && WordDictionary.Contains(lower[..^3]))
        return true;
      if (lower.EndsWith("ers") && WordDictionary.Contains(lower[..^1])) // honeymooners → honeymooner
        return true;
      return false;
    }

    // Characters that are upside-down versions of Latin letters — words made of these need reversing
    private static readonly HashSet<char> UpsideDownChars =
    [
      '\u0250', // ɐ→a
      '\u01DD', // ǝ→e
      '\u0279', // ɹ→r
      '\u0265', // ɥ→h
      '\u026F', // ɯ→m
      '\u0287', // ʇ→t
      '\u028E', // ʎ→y
      '\u028C', // ʌ→v
      '\u028D', // ʍ→w
      '\u0254', // ɔ→c
      '\u025F', // ɟ→f
      '\u029E', // ʞ→k
      '\u0253', // ɓ→b
      '\u0285', // ʅ→l
      '\u1D09', // ᴉ→i
      '\uA72D', // ꜭ→D variant
      '\uA4F7', // ꓷ→d
      '\uA4D8', // ꓘ→K
      '\uA4E9', // ꓩ→Z
    ];

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>File extensions that indicate the title is a filename, not obfuscated text.</summary>
    private static readonly string[] FileExtensions = [".mp4", ".avi", ".mkv", ".wmv", ".mov", ".flv", ".webm", ".m4v"];

    public bool IsObfuscated(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return false;

      // Filenames are not obfuscated — they just happen to contain metadata
      // Check original, confusable-decoded, and Cyrillic-lookalike variants
      // Cyrillic р(U+0440) looks like p, so .mр4 = .mp4
      var titleForExtCheck = title
        .Replace('\u0440', 'p')  // Cyrillic р → p
        .Replace('\u0435', 'e')  // Cyrillic е → e
        .Replace('\u043E', 'o')  // Cyrillic о → o
        .Replace('\u0430', 'a'); // Cyrillic а → a
      if (FileExtensions.Any(ext => titleForExtCheck.Contains(ext, StringComparison.OrdinalIgnoreCase)))
        return false;

      // Titles with significant CJK/Japanese/Korean content are not leet-obfuscated
      // (they may contain catalog codes like "Test339B天月あず" which should not be decoded)
      int cjkChars = title.Count(c => c >= 0x3000 && c <= 0x9FFF || c >= 0xAC00 && c <= 0xD7AF || c >= 0x3040 && c <= 0x30FF);
      if (cjkChars >= 3)
        return false;

      int suspiciousChars = 0;
      int leetTransitions = 0;

      for (int i = 0; i < title.Length; i++)
      {
        char c = title[i];

        // Count non-basic-latin characters (above U+00FF)
        if (c > 0x00FF)
          suspiciousChars++;

        // Count surrogate pairs (mathematical symbols etc.)
        if (char.IsHighSurrogate(c))
          suspiciousChars++;

        // Count leet-speak transitions (digit adjacent to letter)
        if (i > 0 && LeetMap.ContainsKey(c) && char.IsLetter(title[i - 1]))
          leetTransitions++;
        if (i > 0 && char.IsLetter(c) && LeetMap.ContainsKey(title[i - 1]))
          leetTransitions++;
      }

      return suspiciousChars > 0 || leetTransitions > 1;
    }

    private int CountDictionaryWords(string text)
    {
      return text.Split([' ', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
        .Count(w => w.Length >= 3 && w.All(char.IsLetter) && IsInDictionary(w));
    }

    public string? NormalizeTitle(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return null;

      EnsureDynamicDictionary();

      // Always run safe pass (Confusables + Dictionary i/l fix — can't damage anything)
      var safe = NormalizeSafe(title);

      // Always run full pass (Leet + Reversed + Alt-Leet)
      var full = NormalizeFull(title);

      // Pick the best result:
      // 1. If full decode improved dict word count → use full (even if not "obfuscated")
      // 2. If full decode didn't help but safe did → use safe
      // 3. If nothing helped → null
      int origWords = CountDictionaryWords(title);

      bool obfuscated = IsObfuscated(title);

      if (full != title)
      {
        int fullWords = CountDictionaryWords(full);
        // If known obfuscated: accept if dict words maintained or improved (>= trust the decoder)
        // If not known obfuscated: must strictly improve (> prevents damage to clean titles)
        if (obfuscated ? fullWords >= origWords : fullWords > origWords)
          return full;
      }

      if (safe != title)
      {
        int safeWords = CountDictionaryWords(safe);
        // Safe pass: accept if it maintains or improves (>= is fine, safe can't damage)
        if (safeWords >= origWords)
          return safe;
      }

      return null;
    }

    /// <summary>
    /// Full normalization pipeline: Unicode + Leet + Reversed + Dictionary + Alt-Leet.
    /// Use for titles known to be obfuscated.
    /// </summary>
    private string NormalizeFull(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return title;

      // Decode HTML entities
      title = title.Replace("&#039;", "'").Replace("&amp;", "&")
                   .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");

      EnsureDynamicDictionary();

      var result = NormalizeUnicode(title);
      result = HandleReversedAscii(result);
      result = DecodeLeetSpeak(result);
      result = PostProcessWithDictionary(result);
      result = TryAltLeetFallback(result, title);
      result = CollapseSpaces(result);
      result = ToTitleCase(result);

      return result.Trim();
    }

    public string NormalizeTitleLegacy(string title) => NormalizeFull(title);

    /// <summary>
    /// Safe normalization pass: only Unicode confusables, accents, dictionary i/l correction.
    /// Cannot damage clean titles — no leet decode, no reversed text detection.
    /// </summary>
    private string NormalizeSafe(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return title;

      title = title.Replace("&#039;", "'").Replace("&amp;", "&")
                   .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");

      EnsureDynamicDictionary();

      var result = NormalizeUnicode(title);
      result = PostProcessWithDictionary(result);
      result = CollapseSpaces(result);
      result = ToTitleCase(result);

      return result.Trim();
    }

    public List<(DbStar Star, double Confidence)> DetectStars(string normalizedTitle, List<DbStar> knownStars)
    {
      var results = new List<(DbStar Star, double Confidence)>();
      if (string.IsNullOrWhiteSpace(normalizedTitle)) return results;

      var titleLower = normalizedTitle.ToLowerInvariant();
      var titleWords = titleLower.Split([' ', '-', ',', '.', '(', ')', ':', '\'', '"'], StringSplitOptions.RemoveEmptyEntries);

      foreach (var star in knownStars)
      {
        if (star.Name.Length < 6) continue;

        var starWords = star.Name.ToLowerInvariant()
          .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (starWords.Length >= 2)
        {
          // Two-word name: each word must be at least 3 chars, exact match only
          if (starWords.Any(w => w.Length < 3)) continue;

          bool allMatch = starWords.All(sw => titleWords.Any(tw => tw == sw));
          if (allMatch)
            results.Add((star, 1.0));
        }
        else if (starWords.Length == 1 && starWords[0].Length >= 6)
        {
          // Single-word name: exact match only, min 6 chars
          if (titleWords.Contains(starWords[0]))
            results.Add((star, 1.0));
        }
      }

      return results;
    }

    public List<DbTag> DetectTags(string normalizedTitle, List<DbTag> knownTags)
    {
      if (string.IsNullOrWhiteSpace(normalizedTitle)) return [];

      var titleLower = normalizedTitle.ToLowerInvariant();
      return knownTags
        .Where(t => t.Name.Length >= 4 && titleLower.Contains(t.Name.ToLowerInvariant()))
        .ToList();
    }

    public async Task<int> NormalizeAllTitles(bool forceReprocess = false, bool normalizeTitles = true, bool detectStars = true, bool detectTags = true, NormalizationProgress? progress = null, Action<string, string?, string>? onTitleProcessed = null, CancellationToken ct = default)
    {
      var allVideos = await videoService.GetVideoItems();

      // Info-Log: wie viele obfuskiert?
      var obfuscatedCount = allVideos.Count(v => IsObfuscated(v.Title));

      // Bestimme welche Titel verarbeitet werden:
      // - normalizeTitles: Titel die obfuskiert sind ODER noch kein NormalizedTitle haben
      //   Bei forceReprocess: nur obfuskierte oder fehlende NormalizedTitle (clean bereits verarbeitete überspringen)
      // - detectStars/detectTags: Videos ohne Stars/Tags
      var toNormalize = new List<DbVideoItem>();
      if (normalizeTitles)
      {
        toNormalize = allVideos
          .Where(v => forceReprocess
            ? (IsObfuscated(v.Title) || string.IsNullOrEmpty(v.NormalizedTitle))
            : string.IsNullOrEmpty(v.NormalizedTitle))
          .ToList();
      }

      var toDetectOnly = new List<DbVideoItem>();
      if (detectStars || detectTags)
      {
        var normalizeIds = new HashSet<long>(toNormalize.Select(v => v.Id));
        toDetectOnly = allVideos
          .Where(v => !normalizeIds.Contains(v.Id)
            && ((detectStars && (v.Stars == null || v.Stars.Count == 0))
             || (detectTags && (v.Tags == null || v.Tags.Count == 0))))
          .ToList();
      }

      var toProcess = toNormalize.Concat(toDetectOnly).ToList();

      if (toProcess.Count == 0)
      {
        logger.LogInformation("No titles to process");
        return 0;
      }

      logger.LogInformation("Processing {Count} titles ({Normalize} to normalize + {Detect} detect-only, {Obfuscated} obfuscated total)",
        toProcess.Count, toNormalize.Count, toDetectOnly.Count, obfuscatedCount);

      if (progress != null)
      {
        progress.Total = toProcess.Count;
        progress.Phase = "Initializing...";
      }

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var allStars = detectStars ? await context.Stars.Include(s => s.Videos).ToListAsync(ct) : [];
      var allTags = detectTags ? await context.Tags.Include(t => t.Videos).ToListAsync(ct) : [];

      int processed = 0;
      int starsDetected = 0;
      int tagsDetected = 0;
      int titlesNormalized = 0;
      int unsicher = 0;

      // Phase 1: Normalisierung — alle Titel durch NormalizeTitle() (Zwei-Pass)
      if (normalizeTitles && toNormalize.Count > 0)
      {
        for (int i = 0; i < toNormalize.Count; i++)
        {
          ct.ThrowIfCancellationRequested();
          var video = toNormalize[i];

          var normalized = NormalizeTitle(video.Title);
          if (normalized != null && IsPlausible(normalized))
          {
            { var (s, t) = await SaveNormalization(context, video, normalized, allStars, allTags, detectStars, detectTags, ct); starsDetected += s; tagsDetected += t; }
            onTitleProcessed?.Invoke(video.Title, normalized, "decoder");
            titlesNormalized++;
          }
          else if (normalized != null)
          {
            { var (s, t) = await SaveNormalization(context, video, normalized, allStars, allTags, detectStars, detectTags, ct); starsDetected += s; tagsDetected += t; }
            onTitleProcessed?.Invoke(video.Title, normalized, "decoder-unsicher");
            titlesNormalized++;
            unsicher++;
          }
          else
          {
            // NormalizeTitle returned null — no change, still run star/tag detection on original
            { var (s, t) = await SaveNormalization(context, video, video.Title, allStars, allTags, detectStars, detectTags, ct); starsDetected += s; tagsDetected += t; }
            onTitleProcessed?.Invoke(video.Title, null, "skip");
          }

          processed++;
          if (progress != null)
          {
            progress.Current = processed;
            progress.StarsDetected = starsDetected;
            progress.TagsDetected = tagsDetected;
            progress.TitlesNormalized = titlesNormalized;
            progress.Phase = $"Decoding ({unsicher} uncertain)";
          }

          if (processed % 100 == 0)
            await context.SaveChangesAsync(ct);

          if (processed % 10 == 0)
            await Task.Yield(); // Let UI thread update progress
        }

        await context.SaveChangesAsync(ct);

        logger.LogInformation("Normalization handled {Total} titles ({Normalized} changed, {Unsicher} unsicher)",
          toNormalize.Count, titlesNormalized, unsicher);
      }

      // Phase 2: Star/Tag Erkennung für restliche Videos (ohne Normalisierung)
      if (toDetectOnly.Count > 0)
      {
        if (progress != null)
          progress.Phase = $"Star/Tag detection ({toDetectOnly.Count} titles)";

        foreach (var video in toDetectOnly)
        {
          ct.ThrowIfCancellationRequested();

          var detectedStarList = detectStars
            ? DetectStars(video.Title, allStars).Where(d => d.Confidence >= 0.7).ToList()
            : new List<(DbStar Star, double Confidence)>();
          var detectedTagList = detectTags ? DetectTags(video.Title, allTags) : new List<DbTag>();

          var dbVideo = await context.VideoItems.FindAsync([video.Id], ct);
          if (dbVideo != null)
          {
            foreach (var (star, _) in detectedStarList)
            {
              if (!await context.VideoStars.AnyAsync(vs => vs.VideoId == dbVideo.Id && vs.StarId == star.Id, ct))
              {
                context.VideoStars.Add(new DbVideoStar { VideoId = dbVideo.Id, StarId = star.Id, IsAutoDetected = true });
                starsDetected++;
              }
            }

            foreach (var tag in detectedTagList)
            {
              if (!await context.VideoTags.AnyAsync(vt => vt.VideoId == dbVideo.Id && vt.TagId == tag.Id, ct))
              {
                context.VideoTags.Add(new DbVideoTag { VideoId = dbVideo.Id, TagId = tag.Id, IsAutoDetected = true });
                tagsDetected++;
              }
            }
          }

          processed++;
          if (progress != null)
          {
            progress.Current = processed;
            progress.StarsDetected = starsDetected;
            progress.TagsDetected = tagsDetected;
          }

          if (processed % 100 == 0)
            await context.SaveChangesAsync(ct);

          if (processed % 10 == 0)
            await Task.Yield();
        }

        await context.SaveChangesAsync(ct);
      }

      if (progress != null)
      {
        progress.Phase = "Done";
        progress.Current = processed;
      }

      logger.LogInformation(
        "Normalized {Titles} titles, detected {Stars} stars and {Tags} tags across {Total} videos",
        titlesNormalized, starsDetected, tagsDetected, processed);

      await videoService.ReloadVideos();

      return processed;
    }

    private async Task<(int Stars, int Tags)> SaveNormalization(VrScraperContext context, DbVideoItem video,
      string normalizedTitle, List<DbStar> allStars, List<DbTag> allTags, bool detectStars, bool detectTags,
      CancellationToken ct)
    {
      int stars = 0, tags = 0;
      var detectedStarList = detectStars
        ? DetectStars(normalizedTitle, allStars).Where(d => d.Confidence >= 0.7).ToList()
        : new List<(DbStar Star, double Confidence)>();
      var detectedTagList = detectTags ? DetectTags(normalizedTitle, allTags) : new List<DbTag>();

      var dbVideo = await context.VideoItems.FindAsync([video.Id], ct);
      if (dbVideo == null) return (0, 0);

      dbVideo.NormalizedTitle = normalizedTitle;

      foreach (var (star, _) in detectedStarList)
      {
        if (!await context.VideoStars.AnyAsync(vs => vs.VideoId == dbVideo.Id && vs.StarId == star.Id, ct))
        {
          context.VideoStars.Add(new DbVideoStar { VideoId = dbVideo.Id, StarId = star.Id, IsAutoDetected = true });
          stars++;
        }
      }

      foreach (var tag in detectedTagList)
      {
        if (!await context.VideoTags.AnyAsync(vt => vt.VideoId == dbVideo.Id && vt.TagId == tag.Id, ct))
        {
          context.VideoTags.Add(new DbVideoTag { VideoId = dbVideo.Id, TagId = tag.Id, IsAutoDetected = true });
          tags++;
        }
      }

      return (stars, tags);
    }

    /// <summary>
    /// Checks if a decoded title looks plausible (contains real words).
    /// </summary>
    private bool IsPlausible(string decoded)
    {
      EnsureDynamicDictionary();
      var words = decoded.Split([' ', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
        .Where(w => w.Length >= 3 && w.All(char.IsLetter))
        .ToList();
      if (words.Count == 0) return true; // no testable words, trust decoder
      int dictHits = words.Count(w => IsInDictionary(w));
      return (double)dictHits / words.Count > 0.5;
    }

    // ── Internal Methods ───────────────────────────────────────────────

    private static string NormalizeUnicode(string input)
    {
      // Pre-process: detect upside-down text — returns fully normalized ASCII if detected
      var upsideDown = HandleUpsideDown(input);
      if (upsideDown != null)
        return upsideDown;

      var sb = new StringBuilder(input.Length);

      for (int i = 0; i < input.Length; i++)
      {
        char c = input[i];

        // Handle surrogate pairs (Mathematical Alphanumeric Symbols etc.)
        if (char.IsHighSurrogate(c) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
        {
          int codepoint = char.ConvertToUtf32(c, input[i + 1]);
          if (SurrogatePairConfusables.TryGetValue(codepoint, out var mapped))
          {
            sb.Append(mapped);
          }
          // else: skip unknown surrogate pair (likely emoji/decorator)
          i++; // skip low surrogate
          continue;
        }

        // Handle BMP confusables
        if (Confusables.TryGetValue(c, out var replacement))
        {
          sb.Append(replacement);
          continue;
        }

        // Keep ASCII as-is
        if (c <= 0x007F)
        {
          sb.Append(c);
          continue;
        }

        // Latin Extended with diacritics — try to extract base letter
        // U+00C0-024F covers Latin-1 Supplement, Latin Extended-A, Latin Extended-B
        if (c >= 0x00C0 && c <= 0x024F)
        {
          // Direct accent map lookup first (more reliable than FormKD under InvariantGlobalization)
          if (AccentMap.TryGetValue(c, out var accentMapped))
          {
            sb.Append(accentMapped);
            continue;
          }

          // Try to decompose to base letter (e.g. Ť → T, ė → e, ŕ → r)
          var decomposed = c.ToString().Normalize(System.Text.NormalizationForm.FormKD);
          if (decomposed.Length > 0 && decomposed[0] <= 0x007F)
            sb.Append(decomposed[0]);
          else
            sb.Append(c);
          continue;
        }

        // Combining characters (U+0300-036F) — skip (diacritics on previous char)
        if (c >= 0x0300 && c <= 0x036F)
          continue;

        // Strip everything else (emoji, decorators, unknown Unicode)
        // But keep spaces (including fullwidth space U+3000)
        if (c == ' ' || c == '\u3000')
          sb.Append(' ');
      }

      // Spaced-out text detection: if >60% of "words" are single characters, collapse spaces
      var normalized = sb.ToString();
      var spacedWords = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (spacedWords.Length > 3)
      {
        // Only count single LETTERS (not digits) — "D S 1 5 2 8 2" is a catalog code, not spaced text
        int singleLetterCount = spacedWords.Count(w => w.Length == 1 && char.IsLetter(w[0]));
        if ((double)singleLetterCount / spacedWords.Length > 0.6)
        {
          // Collapse: join single-char runs, keep multi-char words separated
          var collapsed = new StringBuilder(normalized.Length);
          bool prevWasSingle = false;
          foreach (var sw in spacedWords)
          {
            bool isSingle = sw.Length == 1 && char.IsLetterOrDigit(sw[0]);
            if (collapsed.Length > 0 && !(prevWasSingle && isSingle))
              collapsed.Append(' ');
            collapsed.Append(sw);
            prevWasSingle = isSingle;
          }
          return collapsed.ToString();
        }
      }

      return normalized;
    }

    /// <summary>
    /// Detects upside-down text (ɐuɐʅ → anal) and reverses each word's characters.
    /// Only activates when >40% of non-space chars are upside-down characters.
    /// </summary>
    // ASCII chars that flip to different letters when rotated 180°
    private static readonly Dictionary<char, char> UpsideDownAsciiFlip = new()
    {
      ['d'] = 'p', ['p'] = 'd',
      ['b'] = 'q', ['q'] = 'b',
      ['n'] = 'u', ['u'] = 'n',
      ['m'] = 'w', ['w'] = 'm',
      ['D'] = 'P', ['P'] = 'D',
      ['B'] = 'Q', ['Q'] = 'B',
      ['N'] = 'U', ['U'] = 'N',
      ['M'] = 'W', ['W'] = 'M',
    };

    /// <summary>Returns normalized+reversed string if upside-down text detected, null otherwise.</summary>
    private static string? HandleUpsideDown(string input)
    {
      int upsideDownCount = 0;
      int totalChars = 0;
      foreach (var c in input)
      {
        if (c == ' ') continue;
        totalChars++;
        if (UpsideDownChars.Contains(c)) upsideDownCount++;
      }

      if (totalChars == 0 || (double)upsideDownCount / totalChars < 0.3)
        return null; // Not upside-down text

      // Map each char to ASCII; only flip original ASCII chars (not mapped ones)
      var sb = new StringBuilder(input.Length);
      foreach (var c in input)
      {
        if (c == ' ') { sb.Append(' '); continue; }
        if (Confusables.TryGetValue(c, out var mapped))
        {
          // Already correctly mapped by Confusables — don't flip
          sb.Append(mapped);
        }
        else if (c <= 0x007F)
        {
          // Original ASCII char in upside-down context — flip p↔d, b↔q, n↔u
          sb.Append(UpsideDownAsciiFlip.TryGetValue(c, out var flipped) ? flipped : c);
        }
        // skip unmapped non-ASCII
      }

      var words = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      var reversed = words
        .Select(w => new string(w.Reverse().ToArray()))
        .Reverse()
        .ToArray();

      return string.Join(' ', reversed);
    }

    /// <summary>
    /// Detects reversed ASCII text by checking if reversing words yields more dictionary hits.
    /// E.g. "yssup" → "pussy", "kcid" → "dick"
    /// Must be called after NormalizeUnicode() and before DecodeLeetSpeak().
    /// </summary>
    private string HandleReversedAscii(string input)
    {
      var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (words.Length == 0) return input;

      // Only consider ASCII-letter words for reversal check
      int forwardHits = 0;
      int reverseHits = 0;
      int testableWords = 0;

      foreach (var word in words)
      {
        if (word.Length < 3 || !word.All(c => char.IsLetter(c) && c <= 0x007F))
          continue;

        testableWords++;
        var lower = word.ToLowerInvariant();
        if (IsInDictionary(lower)) forwardHits++;

        var reversed = new string(lower.Reverse().ToArray());
        if (IsInDictionary(reversed)) reverseHits++;
      }

      // Only reverse if reverse hits are significantly better
      if (testableWords < 2 || reverseHits <= forwardHits)
        return input;

      // Reverse all words and their order
      var reversedWords = words
        .Select(w =>
        {
          if (w.Length < 2 || !w.All(c => char.IsLetter(c) && c <= 0x007F))
            return w;
          var chars = w.ToCharArray();
          // Preserve case pattern: if first char was upper, make first char of reversed upper
          bool firstWasUpper = char.IsUpper(chars[0]);
          Array.Reverse(chars);
          var rev = new string(chars).ToLowerInvariant();
          if (firstWasUpper && rev.Length > 0)
            rev = char.ToUpperInvariant(rev[0]) + rev[1..];
          return rev;
        })
        .Reverse()
        .ToArray();

      return string.Join(' ', reversedWords);
    }

    /// <summary>Regex to detect date patterns that should not be leet-decoded.</summary>
    private static readonly Regex DatePattern = new(@"\d{1,4}[.\-/]\d{1,2}[.\-/]\d{1,4}", RegexOptions.Compiled);

    /// <summary>Regex for ordinals (1st, 2nd, 3rd, etc.) and digit-abbreviations (4some, 3way).</summary>
    private static readonly Regex ProtectedPattern = new(@"(?<=^|\s)\d+(st|nd|rd|th|some|way)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PureNumberToken = new(@"(?<=^|\s)\d+(?=\s|$|-)", RegexOptions.Compiled);

    private static bool IsLetterOrLeet(char c) => char.IsLetter(c) || LeetMap.ContainsKey(c);

    private static string DecodeLeetSpeak(string input)
    {
      // Protect date patterns, ordinals, and digit-abbreviations from leet-decoding
      var protectedInput = input;
      var placeholders = new List<(string placeholder, string original)>();
      int placeholderIdx = 0;

      foreach (var pattern in new[] { DatePattern, ProtectedPattern, PureNumberToken })
      {
        var matches = pattern.Matches(protectedInput);
        for (int d = matches.Count - 1; d >= 0; d--)
        {
          var match = matches[d];
          var placeholder = $"\x01P{placeholderIdx++}\x01";
          placeholders.Add((placeholder, match.Value));
          protectedInput = protectedInput[..match.Index] + placeholder + protectedInput[(match.Index + match.Length)..];
        }
      }

      // Multi-char replacements first
      var result = protectedInput.Replace("vv", "w", StringComparison.OrdinalIgnoreCase);

      // Single-char leet decode — treat other leet chars as valid neighbors
      // so consecutive leet like "61@k3" decodes fully to "blake"
      var sb = new StringBuilder(result.Length);
      for (int i = 0; i < result.Length; i++)
      {
        char c = result[i];

        // Skip entire placeholder sequences (\x01...\x01)
        if (c == '\x01')
        {
          sb.Append(c);
          i++;
          while (i < result.Length && result[i] != '\x01') { sb.Append(result[i]); i++; }
          if (i < result.Length) sb.Append(result[i]); // closing \x01
          continue;
        }

        if (LeetMap.TryGetValue(c, out var leetChar))
        {
          bool prevValid = i > 0 && IsLetterOrLeet(result[i - 1]);
          bool nextValid = i + 1 < result.Length && IsLetterOrLeet(result[i + 1]);

          // '!' is only leet when BOTH neighbors are actual letters (not other leet/punctuation)
          // "h!llo" → decode, "hello!" → punctuation, "!!!" → punctuation
          if (c == '!' && !(i > 0 && char.IsLetter(result[i - 1]) && i + 1 < result.Length && char.IsLetter(result[i + 1])))
          {
            sb.Append(c);
            continue;
          }

          // '@' and '$' should always be decoded (even standalone " @ " = "a", "word$" = "words")
          // Other leet chars need at least one letter/leet neighbor
          bool alwaysDecode = c == '@' || c == '$';
          if (alwaysDecode || prevValid || nextValid)
          {
            // Special case: '1' is ambiguous (l or i)
            // At word start (after space/punctuation or string start) → 'l' (like "1ittle")
            // Otherwise → 'i' (like "L1sa", "N1cole", "P1nelli")
            if (c == '1')
            {
              bool atWordStart = i == 0 || result[i - 1] == ' ' || result[i - 1] == '-';
              sb.Append(atWordStart ? 'l' : 'i');
            }
            else
            {
              sb.Append(leetChar);
            }
          }
          else
          {
            sb.Append(c);
          }
        }
        else
        {
          sb.Append(c);
        }
      }

      // Restore protected patterns
      var decoded = sb.ToString();
      foreach (var (placeholder, original) in placeholders)
      {
        decoded = decoded.Replace(placeholder, original);
      }

      return decoded;
    }

    private static string CollapseSpaces(string input)
    {
      var sb = new StringBuilder(input.Length);
      bool lastWasSpace = false;
      foreach (var c in input)
      {
        if (c == ' ')
        {
          if (!lastWasSpace) sb.Append(' ');
          lastWasSpace = true;
        }
        else
        {
          sb.Append(c);
          lastWasSpace = false;
        }
      }
      return sb.ToString();
    }

    private static string ToTitleCase(string input)
    {
      if (string.IsNullOrWhiteSpace(input)) return input;

      var words = input.Split(' ');
      for (int i = 0; i < words.Length; i++)
      {
        if (words[i].Length > 0)
        {
          words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
        }
      }
      return string.Join(' ', words);
    }

    /// <summary>
    /// Post-processes decoded text using dictionary lookup to resolve ambiguous characters.
    /// E.g. "Biake" → try I→l → "Blake" (in dictionary) → use "Blake"
    /// </summary>
    private string PostProcessWithDictionary(string input)
    {
      var words = input.Split(' ');
      var result = new string[words.Length];

      for (int w = 0; w < words.Length; w++)
      {
        var word = words[w];

        // Handle hyphenated words (e.g. "step-dad")
        if (word.Contains('-'))
        {
          var parts = word.Split('-');
          var corrected = parts.Select(p => CorrectWord(p)).ToArray();
          result[w] = string.Join('-', corrected);
        }
        else
        {
          result[w] = CorrectWord(word);
        }
      }

      return string.Join(' ', result);
    }

    /// <summary>
    /// Alt-Leet fallback: for words still not in dictionary after standard leet decoding,
    /// try alternate leet mappings (e.g. 4→e instead of 4→a) and check the dictionary.
    /// </summary>
    private static readonly Dictionary<char, char[]> AltLeetMap = new()
    {
      ['4'] = ['e'],       // 4→a is default, try 4→e (e.g. "h4r" → "her" instead of "har")
      ['0'] = ['u'],       // 0→o is default, try 0→u
      ['3'] = ['a'],       // 3→e is default, try 3→a
      ['1'] = ['i', 'l'],  // 1→l/i context-dependent, try the other
      ['5'] = ['z'],       // 5→s is default, try 5→z
      ['6'] = ['g'],       // 6→b is default, try 6→g
      ['9'] = ['p'],       // 9→g is default, try 9→p
    };

    private static readonly Regex WordSplitPattern = new(@"([\s\-/,;:!?().&'""]+)", RegexOptions.Compiled);

    private string TryAltLeetFallback(string processed, string originalTitle)
    {
      var processedParts = WordSplitPattern.Split(processed);
      var originalParts = WordSplitPattern.Split(originalTitle);

      // Build a quick lookup of original words by approximate position
      var result = new string[processedParts.Length];

      for (int w = 0; w < processedParts.Length; w++)
      {
        var pWord = processedParts[w];
        result[w] = pWord;

        // Skip separators, short words, already-correct words
        if (pWord.Length < 3 || !pWord.Any(char.IsLetter) || !pWord.All(char.IsLetter) || IsInDictionary(pWord))
          continue;

        // Try to find the corresponding original word (by position, approximately)
        string? origWord = w < originalParts.Length ? originalParts[w] : null;
        if (origWord == null) continue;

        // Check if original had leet chars that could have alternate mappings
        var hasAltLeet = origWord.Any(c => AltLeetMap.ContainsKey(c));
        if (!hasAltLeet) continue;

        // Try alternate leet mappings
        var bestCandidate = TryAltLeetCombinations(origWord);
        if (bestCandidate != null)
          result[w] = bestCandidate;
      }

      return string.Concat(result);
    }

    /// <summary>
    /// Tries alternate leet decode combinations for a single word and returns
    /// the first dictionary hit, or null if none found.
    /// </summary>
    private string? TryAltLeetCombinations(string word)
    {
      // Find positions with alt-leet chars
      var altPositions = new List<(int pos, char orig, char[] alts)>();
      for (int i = 0; i < word.Length; i++)
      {
        if (AltLeetMap.TryGetValue(word[i], out var alts))
          altPositions.Add((i, word[i], alts));
      }

      if (altPositions.Count == 0 || altPositions.Count > 5)
        return null;

      // For each alt-leet position, also include the standard leet decode as an option
      var options = new List<(int pos, char[] choices)>();
      foreach (var (pos, orig, alts) in altPositions)
      {
        var choices = new List<char>();
        if (LeetMap.TryGetValue(orig, out var standard))
          choices.Add(standard);
        choices.AddRange(alts);
        options.Add((pos, choices.Distinct().ToArray()));
      }

      // Also decode non-alt leet chars normally
      var baseChars = word.ToCharArray();
      for (int i = 0; i < baseChars.Length; i++)
      {
        if (!altPositions.Any(ap => ap.pos == i) && LeetMap.TryGetValue(baseChars[i], out var lc))
        {
          bool prevValid = i > 0 && IsLetterOrLeet(baseChars[i - 1]);
          bool nextValid = i + 1 < baseChars.Length && IsLetterOrLeet(baseChars[i + 1]);
          if (prevValid || nextValid)
            baseChars[i] = lc;
        }
      }

      // Try all combinations of alt positions (limit explosion)
      int totalCombos = 1;
      foreach (var (_, choices) in options)
        totalCombos *= choices.Length;
      if (totalCombos > 64) return null;

      for (int combo = 0; combo < totalCombos; combo++)
      {
        var attempt = (char[])baseChars.Clone();
        int divisor = 1;
        for (int o = 0; o < options.Count; o++)
        {
          var (pos, choices) = options[o];
          int choiceIdx = (combo / divisor) % choices.Length;
          attempt[pos] = choices[choiceIdx];
          divisor *= choices.Length;
        }

        var candidate = new string(attempt).ToLowerInvariant();

        // Direct dictionary check
        if (IsInDictionary(candidate))
        {
          var result = attempt;
          for (int i = 0; i < result.Length; i++)
          {
            if (char.IsLetter(word[i]) && char.IsUpper(word[i]))
              result[i] = char.ToUpperInvariant(result[i]);
          }
          return new string(result);
        }

        // Also try canonical index (resolves i/l, v/u, b/g on top of alt-leet)
        var canon = Canonicalize(candidate);
        if (CanonicalIndex.TryGetValue(canon, out var canonCandidates))
        {
          var best = canonCandidates.FirstOrDefault(c => c.Length == candidate.Length) ?? canonCandidates[0];
          var result = best.ToCharArray();
          for (int i = 0; i < Math.Min(result.Length, word.Length); i++)
          {
            if (char.IsUpper(word[i]))
              result[i] = char.ToUpperInvariant(result[i]);
          }
          return new string(result);
        }
      }

      return null;
    }

    private string CorrectWord(string word)
    {
      if (word.Length < 2) return word;
      var lower = word.ToLowerInvariant();
      if (IsInDictionary(lower)) return word;

      var canon = Canonicalize(lower);
      if (CanonicalIndex.TryGetValue(canon, out var candidates))
      {
        var best = candidates.FirstOrDefault(c => c.Length == lower.Length) ?? candidates[0];
        var result = word.ToCharArray();
        for (int i = 0; i < Math.Min(result.Length, best.Length); i++)
          result[i] = char.IsUpper(word[i]) ? char.ToUpperInvariant(best[i]) : best[i];
        return new string(result);
      }

      // Try with inflection stripping: "rldes" → strip "s" → canon("rlde") → find "ride" → rebuild "rides"
      string[] suffixes = ["s", "es", "ed", "ing", "ers", "er"];
      foreach (var suffix in suffixes)
      {
        if (lower.Length > suffix.Length + 2 && lower.EndsWith(suffix))
        {
          var stem = lower[..^suffix.Length];
          var stemCanon = Canonicalize(stem);
          if (CanonicalIndex.TryGetValue(stemCanon, out var stemCandidates))
          {
            var best = stemCandidates.FirstOrDefault(c => c.Length == stem.Length) ?? stemCandidates[0];
            var corrected = best + suffix;
            var result = word.ToCharArray();
            for (int i = 0; i < Math.Min(result.Length, corrected.Length); i++)
              result[i] = char.IsUpper(word[i]) ? char.ToUpperInvariant(corrected[i]) : corrected[i];
            return new string(result);
          }
        }
      }

      // Also check dynamic dictionary with canonical lookup
      var dynamicCanon = Canonicalize(lower);
      foreach (var dw in _dynamicDictionary)
      {
        if (Canonicalize(dw) == dynamicCanon)
        {
          var result = word.ToCharArray();
          for (int i = 0; i < Math.Min(result.Length, dw.Length); i++)
          {
            result[i] = char.IsUpper(word[i]) ? char.ToUpperInvariant(dw[i]) : dw[i];
          }
          return new string(result);
        }
      }

      return word;
    }

  }
}
