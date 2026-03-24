using System.Text;
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
      map['\u0253'] = 'b'; // ɓ (used as b upside down)
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

    public bool IsObfuscated(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return false;

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

      return suspiciousChars > 2 || leetTransitions > 4;
    }

    public string NormalizeTitle(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return title;

      var result = NormalizeUnicode(title);
      result = DecodeLeetSpeak(result);
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
        if (star.Name.Length < 6) continue; // Minimum 6 chars total

        var starWords = star.Name.ToLowerInvariant()
          .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (starWords.Length >= 2)
        {
          // Two-word name: each word must be at least 3 chars
          if (starWords.Any(w => w.Length < 3)) continue;

          // Both words must appear in title
          var matchConfidences = new List<double>();
          foreach (var starWord in starWords)
          {
            double bestConf = 0;
            foreach (var titleWord in titleWords)
            {
              var dist = LevenshteinDistance(starWord, titleWord);
              if (dist == 0) bestConf = Math.Max(bestConf, 1.0);
              else if (dist == 1 && starWord.Length >= 4) bestConf = Math.Max(bestConf, 0.8);
              else if (dist == 2 && starWord.Length >= 5) bestConf = Math.Max(bestConf, 0.6);
            }
            matchConfidences.Add(bestConf);
          }

          if (matchConfidences.All(c => c > 0))
          {
            var avgConf = matchConfidences.Average();
            results.Add((star, avgConf));
          }
        }
        else if (starWords.Length == 1)
        {
          // Single-word name: very strict
          var starWord = starWords[0];
          if (starWord.Length < 6) continue;

          foreach (var titleWord in titleWords)
          {
            var dist = LevenshteinDistance(starWord, titleWord);
            if (dist == 0)
            {
              results.Add((star, 1.0));
              break;
            }
            else if (dist == 1 && starWord.Length >= 5)
            {
              results.Add((star, 0.8));
              break;
            }
          }
        }
      }

      return results
        .OrderByDescending(r => r.Confidence)
        .ToList();
    }

    public List<DbTag> DetectTags(string normalizedTitle, List<DbTag> knownTags)
    {
      if (string.IsNullOrWhiteSpace(normalizedTitle)) return [];

      var titleLower = normalizedTitle.ToLowerInvariant();
      return knownTags
        .Where(t => t.Name.Length >= 4 && titleLower.Contains(t.Name.ToLowerInvariant()))
        .ToList();
    }

    public async Task<int> NormalizeAllTitles(bool forceReprocess = false, NormalizationProgress? progress = null)
    {
      var allVideos = await videoService.GetVideoItems();

      // Phase 1: Obfuscated titles — normalize + detect stars/tags
      var obfuscated = allVideos
        .Where(v => IsObfuscated(v.Title) && (forceReprocess || string.IsNullOrEmpty(v.NormalizedTitle)))
        .ToList();

      // Phase 2: All non-obfuscated videos — try star/tag detection from original title
      var nonObfuscatedIds = new HashSet<long>(obfuscated.Select(v => v.Id));
      var withoutStarsOrTags = allVideos
        .Where(v => !nonObfuscatedIds.Contains(v.Id)
          && ((v.Stars == null || v.Stars.Count == 0) || (v.Tags == null || v.Tags.Count == 0)))
        .ToList();

      var toProcess = obfuscated.Concat(withoutStarsOrTags).ToList();

      if (toProcess.Count == 0)
      {
        logger.LogInformation("No titles to process");
        return 0;
      }

      logger.LogInformation("Processing {Count} titles ({Obfuscated} obfuscated + {Normal} normal without stars/tags)",
        toProcess.Count, obfuscated.Count, withoutStarsOrTags.Count);

      // Report total immediately so UI can show progress bar
      if (progress != null)
      {
        progress.Total = toProcess.Count;
        progress.Phase = "Analyzing titles";
      }

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var allStars = await context.Stars.Include(s => s.Videos).ToListAsync();
      var allTags = await context.Tags.Include(t => t.Videos).ToListAsync();

      int processed = 0;
      int starsDetected = 0;
      int tagsDetected = 0;
      int titlesNormalized = 0;

      var obfuscatedIds = new HashSet<long>(obfuscated.Select(v => v.Id));

      // Phase A: CPU-intensive work in parallel (normalize + detect — no DB writes)
      var results = new System.Collections.Concurrent.ConcurrentBag<(
        long VideoId, bool IsObfuscated, string? NormalizedTitle, string TitleForDetection,
        List<(long StarId, double Confidence)> DetectedStarIds,
        List<long> DetectedTagIds)>();

      var starList = allStars.ToList(); // snapshot for thread-safe read
      var tagList = allTags.ToList();

      var totalCount = toProcess.Count;

      // Run parallel analysis with periodic progress reporting
      var parallelTask = Task.Run(() =>
      {
        Parallel.ForEach(toProcess, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, video =>
        {
          bool isObf = obfuscatedIds.Contains(video.Id);
          string? normalizedTitle = null;
          string titleForDetection;

          if (isObf)
          {
            normalizedTitle = NormalizeTitle(video.Title);
            titleForDetection = normalizedTitle;
          }
          else
          {
            titleForDetection = video.Title;
          }

          var detectedStars = DetectStars(titleForDetection, starList)
            .Where(d => d.Confidence >= 0.7)
            .Select(d => (d.Star.Id, d.Confidence))
            .ToList();

          var detectedTagIds = DetectTags(titleForDetection, tagList)
            .Select(t => t.Id)
            .ToList();

          results.Add((video.Id, isObf, normalizedTitle, titleForDetection, detectedStars, detectedTagIds));

          var p = Interlocked.Increment(ref processed);
          if (progress != null)
            progress.Current = p;
        });
      });

      await parallelTask;

      logger.LogInformation("Parallel analysis done for {Count} titles, now persisting to DB...", results.Count);
      if (progress != null)
      {
        progress.Phase = "Saving to database";
        progress.Current = 0;
        progress.Total = results.Count;
      }

      // Phase B: Sequential DB writes (EF Core is not thread-safe)
      int dbProcessed = 0;
      foreach (var result in results)
      {
        var dbVideo = await context.VideoItems.FindAsync(result.VideoId);
        if (dbVideo == null) continue;

        if (result.IsObfuscated && result.NormalizedTitle != null)
          dbVideo.NormalizedTitle = result.NormalizedTitle;

        // Link detected stars
        foreach (var (starId, confidence) in result.DetectedStarIds)
        {
          var star = allStars.FirstOrDefault(s => s.Id == starId);
          if (star == null) continue;
          if (!await context.VideoStars.AnyAsync(vs => vs.VideoId == dbVideo.Id && vs.StarId == starId))
          {
            context.VideoStars.Add(new DbVideoStar { VideoId = dbVideo.Id, StarId = starId, IsAutoDetected = true });
            starsDetected++;
          }
        }

        // Link detected tags
        foreach (var tagId in result.DetectedTagIds)
        {
          var tag = allTags.FirstOrDefault(t => t.Id == tagId);
          if (tag == null) continue;
          if (!await context.VideoTags.AnyAsync(vt => vt.VideoId == dbVideo.Id && vt.TagId == tagId))
          {
            context.VideoTags.Add(new DbVideoTag { VideoId = dbVideo.Id, TagId = tagId, IsAutoDetected = true });
            tagsDetected++;
          }
        }

        if (result.IsObfuscated && result.NormalizedTitle != null)
          titlesNormalized++;

        dbProcessed++;
        if (progress != null)
        {
          progress.Current = dbProcessed;
          progress.StarsDetected = starsDetected;
          progress.TagsDetected = tagsDetected;
          progress.TitlesNormalized = titlesNormalized;
        }
        if (dbProcessed % 100 == 0)
        {
          await context.SaveChangesAsync();
        }
      }

      await context.SaveChangesAsync();

      if (progress != null)
      {
        progress.Phase = "Done";
        progress.Current = results.Count;
      }

      logger.LogInformation(
        "Normalized {Titles} titles, detected {Stars} stars and {Tags} tags across {Total} videos",
        titlesNormalized, starsDetected, tagsDetected, processed);

      await videoService.ReloadVideos();

      return processed;
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
        // But keep spaces
        if (c == ' ')
          sb.Append(c);
      }

      return sb.ToString();
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
      ['D'] = 'P', ['P'] = 'D',
      ['B'] = 'Q', ['Q'] = 'B',
      ['N'] = 'U', ['U'] = 'N',
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

    private static bool IsLetterOrLeet(char c) => char.IsLetter(c) || LeetMap.ContainsKey(c);

    private static string DecodeLeetSpeak(string input)
    {
      // Multi-char replacements first
      var result = input.Replace("vv", "w", StringComparison.OrdinalIgnoreCase);

      // Single-char leet decode — treat other leet chars as valid neighbors
      // so consecutive leet like "61@k3" decodes fully to "blake"
      var sb = new StringBuilder(result.Length);
      for (int i = 0; i < result.Length; i++)
      {
        char c = result[i];

        if (LeetMap.TryGetValue(c, out var leetChar))
        {
          bool prevValid = i > 0 && IsLetterOrLeet(result[i - 1]);
          bool nextValid = i + 1 < result.Length && IsLetterOrLeet(result[i + 1]);

          // Decode if at least one neighbor is a letter or another leet char
          if (prevValid || nextValid)
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

      return sb.ToString();
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

    private static int LevenshteinDistance(string s, string t)
    {
      int n = s.Length, m = t.Length;
      if (n == 0) return m;
      if (m == 0) return n;

      var d = new int[n + 1, m + 1];
      for (int i = 0; i <= n; i++) d[i, 0] = i;
      for (int j = 0; j <= m; j++) d[0, j] = j;

      for (int i = 1; i <= n; i++)
      {
        for (int j = 1; j <= m; j++)
        {
          int cost = s[i - 1] == t[j - 1] ? 0 : 1;
          d[i, j] = Math.Min(
            Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
            d[i - 1, j - 1] + cost);
        }
      }
      return d[n, m];
    }
  }
}
