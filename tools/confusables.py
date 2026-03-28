# Auto-generated from TitleNormalizationService.cs

# Unicode char → ASCII char (BMP + surrogate pairs combined, using Python strings for all)
# BMP chars use direct Unicode literals; chars above U+FFFF use chr() notation.
CONFUSABLES: dict[str, str] = {

    # ── Circled Latin Capital (Ⓐ-Ⓩ) U+24B6-24CF ──────────────────────────
    **{chr(0x24B6 + i): chr(ord('A') + i) for i in range(26)},
    # Circled Latin Small (ⓐ-ⓩ) U+24D0-24E9
    **{chr(0x24D0 + i): chr(ord('a') + i) for i in range(26)},

    # Fullwidth Latin Capital (Ａ-Ｚ) U+FF21-FF3A
    **{chr(0xFF21 + i): chr(ord('A') + i) for i in range(26)},
    # Fullwidth Latin Small (ａ-ｚ) U+FF41-FF5A
    **{chr(0xFF41 + i): chr(ord('a') + i) for i in range(26)},
    # Fullwidth Digits (０-９) U+FF10-FF19
    **{chr(0xFF10 + i): chr(ord('0') + i) for i in range(10)},

    # ── Mathematical Alphanumeric Symbols U+1D400-1D7FF (ex-surrogate pairs) ─
    # Mathematical Bold Capital (𝐀-𝐙) U+1D400-1D419
    **{chr(0x1D400 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Bold Small (𝐚-𝐳) U+1D41A-1D433
    **{chr(0x1D41A + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Italic Capital U+1D434-1D44D
    **{chr(0x1D434 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Italic Small U+1D44E-1D467
    **{chr(0x1D44E + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Bold Italic Capital U+1D468-1D481
    **{chr(0x1D468 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Bold Italic Small U+1D482-1D49B
    **{chr(0x1D482 + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Script Capital U+1D49C-1D4B5
    **{chr(0x1D49C + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Script Small U+1D4B6-1D4CF
    **{chr(0x1D4B6 + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Bold Script Capital U+1D4D0-1D4E9
    **{chr(0x1D4D0 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Bold Script Small U+1D4EA-1D503
    **{chr(0x1D4EA + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Fraktur Capital U+1D504-1D51D
    **{chr(0x1D504 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Fraktur Small U+1D51E-1D537
    **{chr(0x1D51E + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Double-Struck Capital U+1D538-1D551
    **{chr(0x1D538 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Double-Struck Small U+1D552-1D56B
    **{chr(0x1D552 + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Bold Fraktur Capital U+1D56C-1D585
    **{chr(0x1D56C + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Bold Fraktur Small U+1D586-1D59F
    **{chr(0x1D586 + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Sans-Serif Capital U+1D5A0-1D5B9
    **{chr(0x1D5A0 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Sans-Serif Small U+1D5BA-1D5D3
    **{chr(0x1D5BA + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Sans-Serif Bold Capital U+1D5D4-1D5ED
    **{chr(0x1D5D4 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Sans-Serif Bold Small U+1D5EE-1D607
    **{chr(0x1D5EE + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Sans-Serif Italic Capital U+1D608-1D621
    **{chr(0x1D608 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Sans-Serif Italic Small U+1D622-1D63B
    **{chr(0x1D622 + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Sans-Serif Bold Italic Capital U+1D63C-1D655
    **{chr(0x1D63C + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Sans-Serif Bold Italic Small U+1D656-1D66F
    **{chr(0x1D656 + i): chr(ord('a') + i) for i in range(26)},
    # Mathematical Monospace Capital U+1D670-1D689
    **{chr(0x1D670 + i): chr(ord('A') + i) for i in range(26)},
    # Mathematical Monospace Small U+1D68A-1D6A3
    **{chr(0x1D68A + i): chr(ord('a') + i) for i in range(26)},

    # Mathematical Bold Digits U+1D7CE-1D7D7
    **{chr(0x1D7CE + i): chr(ord('0') + i) for i in range(10)},
    # Double-Struck Digits U+1D7D8-1D7E1
    **{chr(0x1D7D8 + i): chr(ord('0') + i) for i in range(10)},
    # Sans-Serif Digits U+1D7E2-1D7EB
    **{chr(0x1D7E2 + i): chr(ord('0') + i) for i in range(10)},
    # Sans-Serif Bold Digits U+1D7EC-1D7F5
    **{chr(0x1D7EC + i): chr(ord('0') + i) for i in range(10)},
    # Monospace Digits U+1D7F6-1D7FF
    **{chr(0x1D7F6 + i): chr(ord('0') + i) for i in range(10)},

    # Old Italic 𐌵 U+10335 → u (seen in DB for "pussy")
    chr(0x10335): 'u',

    # Enclosed Alphanumeric Supplement (emoji-style letters)
    # Squared Latin Capital Letters: 🄰-🅉 U+1F130-1F149
    **{chr(0x1F130 + i): chr(ord('A') + i) for i in range(26)},
    # Negative Circled Latin Capital Letters: 🅐-🅩 U+1F150-1F169
    **{chr(0x1F150 + i): chr(ord('A') + i) for i in range(26)},
    # Negative Squared Latin Capital Letters: 🅰-🆉 U+1F170-1F189
    **{chr(0x1F170 + i): chr(ord('A') + i) for i in range(26)},
    # Regional Indicator Symbols: 🇦-🇿 U+1F1E6-1F1FF (used in flag emoji but also as letters)
    **{chr(0x1F1E6 + i): chr(ord('A') + i) for i in range(26)},

    # ── Small Caps U+1D00+ ────────────────────────────────────────────────
    '\u1D00': 'A',  # ᴀ
    '\u0299': 'B',  # ʙ
    '\u1D04': 'C',  # ᴄ
    '\u1D05': 'D',  # ᴅ
    '\u1D07': 'E',  # ᴇ
    '\u0493': 'F',  # ғ (Cyrillic, used as F)
    '\u0262': 'G',  # ɢ
    '\u029C': 'H',  # ʜ
    '\u026A': 'I',  # ɪ
    '\u1D0A': 'J',  # ᴊ
    '\u1D0B': 'K',  # ᴋ
    '\u029F': 'L',  # ʟ
    '\u1D0D': 'M',  # ᴍ
    '\u0274': 'N',  # ɴ
    '\u1D0F': 'O',  # ᴏ
    '\u1D18': 'P',  # ᴘ
    # no small cap Q
    '\u0280': 'R',  # ʀ
    '\u0A51': 'S',  # (rare)
    '\u1D1B': 'T',  # ᴛ
    '\u1D1C': 'U',  # ᴜ
    '\u1D20': 'V',  # ᴠ
    '\u1D21': 'W',  # ᴡ
    # no small cap X
    '\u028F': 'Y',  # ʏ
    '\u1D22': 'Z',  # ᴢ

    # ── Coptic confusables ────────────────────────────────────────────────
    '\u2C80': 'A',  # Ⲁ
    '\u2C81': 'a',  # ⲁ
    '\u2C82': 'B',  # Ⲃ
    '\u2C83': 'b',  # ⲃ
    '\u2C84': 'G',  # Ⲅ
    '\u2C89': 'e',  # ⲉ
    '\u2C8F': 'h',  # ⲏ (eta)
    '\u2C8E': 'H',  # Ⲏ
    '\u2C90': 'I',  # Ⲓ
    '\u2C91': 'i',  # ⲓ
    '\u2C9A': 'M',  # Ⲙ
    '\u2C9B': 'm',  # ⲙ (not exact but used)
    '\u2C9C': 'N',  # Ⲛ
    '\u2C9D': 'n',  # ⲛ
    '\u2C9E': 'X',  # Ⲝ
    '\u2CA0': 'O',  # Ⲟ
    '\u2CA1': 'o',  # ⲟ
    '\u2CA2': 'P',  # Ⲡ
    '\u2CA3': 'p',  # ⲡ
    '\u2CA4': 'R',  # Ⲥ (actually Coptic Sima, used as R-like)
    '\u2CA5': 'r',  # ⲥ
    '\u2CA6': 'T',  # Ⲧ
    '\u2CA7': 't',  # ⲧ
    '\u2CA8': 'U',  # Ⲩ
    '\u2CA9': 'u',  # ⲩ
    '\u2CAA': 'F',  # Ⲫ
    '\u2CAB': 'f',  # ⲫ
    '\u2CAC': 'K',  # Ⲭ
    '\u2CAD': 'k',  # ⲭ
    '\u2CB0': 'W',  # Ⲱ
    '\u2CB1': 'w',  # ⲱ
    # Coptic used as 's' in obfuscated titles
    '\u0376': 'S',  # Ϩ → S (seen in DB as Scarlett)

    # ── Canadian Aboriginal Syllabics ─────────────────────────────────────
    '\u15E9': 'A',  # ᗩ
    '\u144E': 'N',  # ᑎ
    '\u14AA': 'L',  # ᒪ
    '\u15EA': 'D',  # ᗪ
    '\u15F4': 'W',  # ᗴ→W? Actually ᗯ
    '\u15EF': 'W',  # ᗯ
    # '\u1455': 'J',  # ᑕ→C? Actually ᒍ  (overridden below to 'C')
    '\u1455': 'C',  # ᑕ
    '\u1466': 'S',  # ᑦ
    '\u1585': 'Q',  # ᖅ
    '\u1587': 'R',  # ᖇ
    '\u1591': 'F',  # ᖑ? ᑫ→Q
    '\u15B4': 'V',  # ᖴ
    '\u14F0': 'Y',  # ᓰ→? ᓎ
    '\u1597': 'K',  # ᖗ
    '\u14B2': 'G',  # ᒲ→G? ᘜ
    '\u15B2': 'b',  # ᖲ
    '\u1490': 'B',  # ᒐ→B? ᗷ
    '\u15F7': 'B',  # ᗷ
    '\u1D35': 'I',  # ᴵ (modifier)
    '\u1405': 'E',  # ᐅ→? ᗴ
    '\u1D2F': 'V',  # ᐯ
    '\u15AF': 'V',  # ᖯ→V?
    # ᐯ is actually U+142F
    '\u142F': 'V',  # ᐯ
    # '\u1515': 'P',  # ᔕ→S  (overridden below to 'S')
    '\u1515': 'S',  # ᔕ
    '\u1450': 'T',  # ᑐ→T
    '\u1496': 'D',  # ᒖ→D?
    '\u148C': 'Z',  # ᒌ→Z? ᘔ
    '\u161A': 'Z',  # ᘚ→Z? ᘔ
    '\u1614': 'Z',  # ᘔ
    '\u14BA': 'M',  # ᒺ→M? ᗰ
    '\u15F0': 'M',  # ᗰ

    # ── Armenian/Cyrillic confusables ─────────────────────────────────────
    '\u027E': 'r',  # ɾ
    '\u0585': 'o',  # օ
    '\u0282': 's',  # ʂ
    '\u04BD': 'e',  # ҽ
    '\u0561': 'a',  # ա→a
    '\u0567': 'e',  # է→e (Armenian Ech)
    '\u056B': 'i',  # ի
    '\u0578': 'n',  # ո→n
    '\u057D': 's',  # ս→s (Armenian Now)
    '\u0584': 'q',  # ք→q
    '\u0566': 'g',  # զ→g?
    '\u0581': 'c',  # ց→c
    '\u0571': 'd',  # ձ→d
    '\u0575': 'y',  # յ→y
    '\u0574': 'm',  # մ→m
    '\u0570': 'h',  # հ→h
    '\u0569': 't',  # թ→t

    # ── Cyrillic confusables ──────────────────────────────────────────────
    '\u0430': 'a',  # а
    '\u0435': 'e',  # е
    '\u043E': 'o',  # о
    '\u0440': 'r',  # р (Cyrillic er — was mapped to p, but visually used as r too)
    '\u0441': 'c',  # с
    '\u0443': 'y',  # у→y
    '\u0445': 'x',  # х→x
    '\u0410': 'A',  # А
    '\u0412': 'B',  # В→B
    '\u0415': 'E',  # Е
    '\u041A': 'K',  # К
    '\u041C': 'M',  # М
    '\u041D': 'H',  # Н→H
    '\u041E': 'O',  # О
    '\u0420': 'P',  # Р
    '\u0421': 'C',  # С
    '\u0422': 'T',  # Т
    '\u0425': 'X',  # Х
    # More Cyrillic confusables (used in mixed-script obfuscation)
    '\u0454': 'e',  # є (Cyrillic ie)
    '\u0455': 's',  # ѕ (Cyrillic dze)
    '\u0432': 'b',  # в (Cyrillic ve → used as b visually)
    '\u0456': 'i',  # і (Cyrillic i)
    '\u0457': 'i',  # ї (Cyrillic yi)
    '\u0442': 't',  # т (Cyrillic te)
    '\u043D': 'n',  # н (Cyrillic en)
    '\u0448': 'w',  # ш (Cyrillic sha → w)
    '\u043C': 'm',  # м (Cyrillic em)
    '\u043A': 'k',  # к (Cyrillic ka)

    # ── CJK look-alikes (from actual DB data) ─────────────────────────────
    '\u4E47': 'E',  # 乇
    '\u4E02': 'S',  # 丂
    '\u3112': 'T',  # ㄒ
    '\u5C3A': 'R',  # 尺
    # '\u51E0': 'C',  # 几→N  (overridden below to 'N')
    '\u51E0': 'N',  # 几
    '\u531A': 'C',  # 匚
    '\u5369': 'P',  # 卩
    '\u4E28': 'I',  # 丨
    '\u5E72': 'H',  # 干→H?
    '\u30E2': 'E',  # モ→? not quite
    # More CJK from DB
    '\u3007': 'O',  # 〇 (ideographic zero → O)
    '\u5200': 'D',  # 刀 (knife → D)
    '\u4E39': 'A',  # 丹 → A
    '\uAAB6': 'l',  # ꪶ (Tai Viet → l)

    # ── Upside-down characters ────────────────────────────────────────────
    # (individual char mapping — reversal handled separately)
    '\u01DD': 'e',  # ǝ
    '\u0279': 'r',  # ɹ
    '\u0131': 'i',  # ı (dotless i)
    '\u0250': 'a',  # ɐ
    '\u0254': 'c',  # ɔ (open o, used as c)
    '\u025F': 'f',  # ɟ (looks like f upside down, used as f)
    '\u0265': 'h',  # ɥ
    '\u026F': 'm',  # ɯ
    '\u0270': 'w',  # ɰ
    '\u0287': 't',  # ʇ
    '\u028C': 'v',  # ʌ
    '\u028D': 'w',  # ʍ
    '\u028E': 'y',  # ʎ
    '\u0285': 'l',  # ʅ (squat reversed esh, used as l)
    '\u029E': 'k',  # ʞ
    '\u0253': 'g',  # ɓ (upside-down g — visually looks like rotated g)
    '\u1D09': 'i',  # ᴉ (turned i)
    '\u0183': 'b',  # ƃ
    '\uA72D': 'd',  # ꜭ (turned D variant)
    '\uA4F7': 'd',  # ꓷ (turned D — actual codepoint in DB data)
    '\uA7B0': 'K',  # Ꝁ
    '\uA4D8': 'K',  # ꓘ (turned K)
    '\uA4E9': 'Z',  # ꓩ (turned Z)
    '\u0222': 'S',  # Ȣ→S? (Շ is Armenian)
    '\u0547': '2',  # Շ (Armenian Sha, used as flipped 2)

    # ── Currency/Stroke letter confusables (from actual DB data) ──────────
    '\u20B3': 'A',  # ₳
    '\u0244': 'U',  # Ʉ
    '\u20B4': 'S',  # ₴
    '\u20AE': 'T',  # ₮
    '\u20B1': 'P',  # ₱
    '\u20B5': 'C',  # ₵
    '\u20A3': 'F',  # ₣
    '\u20A5': 'M',  # ₥
    '\u2C67': 'H',  # Ⱨ
    '\u2C68': 'h',  # ⱨ
    '\u024E': 'Y',  # Ɏ
    '\u2C60': 'L',  # Ⱡ
    '\u2C61': 'l',  # ⱡ
    '\u0110': 'D',  # Đ
    '\u0111': 'd',  # đ
    '\u0246': 'E',  # Ɇ
    '\u0247': 'e',  # ɇ
    '\u024C': 'R',  # Ɽ
    '\u024D': 'r',  # ɽ
    '\u2C64': 'R',  # Ɽ (alternate)
    '\u019E': 'n',  # ƞ
    '\u2C66': 't',  # ⱦ
    '\u0142': 'l',  # ł
    '\u0141': 'L',  # Ł
    '\u00D8': 'O',  # Ø
    '\u00F8': 'o',  # ø
    '\u0E3F': 'B',  # ฿ (Thai Baht, used as B)
    '\u20B2': 'G',  # ₲
    '\u20A0': 'E',  # ₠→CE? just E
    '\u20A7': 'P',  # ₧ (Peseta)
    '\u00A2': 'c',  # ¢ → c
    '\u20A6': 'N',  # ₦ (Naira → N)

    # ── Latin Extended-B confusables ──────────────────────────────────────
    '\u0189': 'D',  # Ɖ (African D)
    '\u018E': 'E',  # Ǝ (reversed E)
    '\u018F': 'E',  # Ə (Schwa, used as E)
    '\u0186': 'C',  # Ɔ (Open O, used as C)
    '\u0190': 'E',  # Ɛ (Open E)
    '\u0191': 'F',  # Ƒ
    '\u0193': 'G',  # Ɠ
    '\u0197': 'I',  # Ɨ
    '\u019C': 'M',  # Ɯ (turned M)
    '\u019D': 'N',  # Ɲ
    '\u01A4': 'P',  # Ƥ
    '\u01AC': 'T',  # Ƭ
    '\u01B2': 'V',  # Ʋ
    '\u0224': 'Z',  # Ȥ
    '\u0187': 'C',  # Ƈ
    '\u0198': 'K',  # Ƙ
    '\u01A0': 'O',  # Ơ
    '\u01AF': 'U',  # Ư
    '\u018C': 'd',  # ƌ (d with topbar)
    '\u0192': 'f',  # ƒ (f with hook)
    '\u025D': 'e',  # ɝ (reversed open e)
    '\u0277': 'w',  # ɷ (closed omega, used as w)

    # ── Hebrew used as Latin look-alikes ──────────────────────────────────
    '\u05E0': 'j',  # נ (nun → visually used as j in obfuscated text)
    '\u05D5': 'u',  # ו (vav)

    # ── Greek/Coptic confusables ──────────────────────────────────────────
    '\u03C9': 'w',  # ω (omega)
    '\u03B6': 'z',  # ζ (zeta)
    '\u03C4': 't',  # τ (tau)
    '\u03BD': 'v',  # ν (nu → v)
    '\u03BA': 'k',  # κ (kappa)
    '\u03C1': 'p',  # ρ (rho → p)
    '\u03B7': 'n',  # η (eta → n)
    '\u03C6': 'f',  # φ (phi → f)
    # Greek confusables (uppercase)
    '\u0391': 'A',  # Α
    '\u0392': 'B',  # Β
    '\u0395': 'E',  # Ε
    '\u0396': 'Z',  # Ζ
    '\u0397': 'H',  # Η
    '\u0399': 'I',  # Ι
    '\u039A': 'K',  # Κ
    '\u039C': 'M',  # Μ
    '\u039D': 'N',  # Ν
    '\u039F': 'O',  # Ο
    '\u03A1': 'P',  # Ρ
    '\u03A4': 'T',  # Τ
    '\u03A5': 'Y',  # Υ
    '\u03A7': 'X',  # Χ
    # Greek confusables (lowercase)
    '\u03B1': 'a',  # α
    '\u03B5': 'e',  # ε
    '\u03B9': 'i',  # ι
    '\u03BF': 'o',  # ο
    '\u03C5': 'u',  # υ
    '\u03C3': 's',  # σ (sigma)
    '\u03B2': 'b',  # β (used as b)
    '\u03B4': 'd',  # δ (used as d)
    '\u03B3': 'y',  # γ (used as y)
    # Misc Greek seen in DB
    '\u03DC': 'F',  # Ϝ (Greek digamma)
    '\u03DD': 'f',  # ϝ
    '\u03F2': 'c',  # ϲ (Greek lunate sigma)
    '\u03F9': 'C',  # Ϲ
    '\u03FB': 'M',  # ϻ→M (seen in DB)

    # ── Latin Extended Additional ─────────────────────────────────────────
    '\u0219': 's',  # ș (s with comma below)
    '\u021B': 't',  # ț (t with comma below)
    '\u1E63': 's',  # ṣ
    '\u1E6D': 't',  # ṭ

    # ── Modifier letters / Superscripts ──────────────────────────────────
    '\u1D43': 'a',  # ᵃ
    '\u1D47': 'b',  # ᵇ
    '\u1D9C': 'c',  # ᶜ
    '\u1D48': 'd',  # ᵈ
    '\u1D49': 'e',  # ᵉ
    '\u1DA0': 'f',  # ᶠ
    '\u1D4D': 'g',  # ᵍ
    '\u02B0': 'h',  # ʰ
    '\u2071': 'i',  # ⁱ
    '\u02B2': 'j',  # ʲ
    '\u1D4F': 'k',  # ᵏ
    '\u02E1': 'l',  # ˡ
    '\u1D50': 'm',  # ᵐ
    '\u207F': 'n',  # ⁿ
    '\u1D52': 'o',  # ᵒ
    '\u1D56': 'p',  # ᵖ
    '\u02B3': 'r',  # ʳ
    '\u02E2': 's',  # ˢ
    '\u1D57': 't',  # ᵗ
    '\u1D58': 'u',  # ᵘ
    '\u1D5B': 'v',  # ᵛ
    '\u02B7': 'w',  # ʷ
    '\u02E3': 'x',  # ˣ
    '\u02B8': 'y',  # ʸ

    # ── Subscript digits ──────────────────────────────────────────────────
    '\u2080': '0',  # ₀
    '\u2081': '1',  # ₁
    '\u2082': '2',  # ₂
    '\u2083': '3',  # ₃
    '\u2084': '4',  # ₄
    '\u2085': '5',  # ₅
    '\u2086': '6',  # ₆
    '\u2087': '7',  # ₇
    '\u2088': '8',  # ₈
    '\u2089': '9',  # ₉

    # ── Superscript digits ────────────────────────────────────────────────
    '\u2070': '0',  # ⁰
    '\u00B9': '1',  # ¹
    '\u00B2': '2',  # ²
    '\u00B3': '3',  # ³
    '\u2074': '4',  # ⁴
    '\u2075': '5',  # ⁵
    '\u2076': '6',  # ⁶
    '\u2077': '7',  # ⁷
    '\u2078': '8',  # ⁸
    '\u2079': '9',  # ⁹

    # ── Latin Extended-B / IPA look-alikes ────────────────────────────────
    '\u0127': 'h',  # ħ (used as h)
    '\u026E': 'l',  # ɮ→l?
    '\u0268': 'i',  # ɨ→i
    '\u0289': 'u',  # ʉ→u
    '\u1E9E': 'S',  # ẞ→S
    '\uA7B5': 'r',  # ꞵ→? ꞅ
    '\uA785': 'r',  # ꞅ→r (Latin small letter insular r)

    # ── Letterlike symbols (BMP Script/Fraktur/DoubleStruck gaps) ─────────
    '\u2102': 'C',  # ℂ (Double-Struck C)
    '\u210A': 'g',  # ℊ (Script small g)
    '\u210B': 'H',  # ℋ (Script Capital H)
    '\u210C': 'H',  # ℌ (Fraktur Capital H)
    '\u210D': 'H',  # ℍ (Double-Struck H)
    '\u210E': 'h',  # ℎ (Italic small h / Planck constant)
    '\u2110': 'I',  # ℐ (Script Capital I)
    '\u2111': 'I',  # ℑ (Fraktur Capital I)
    '\u2112': 'L',  # ℒ (Script Capital L)
    '\u2113': 'l',  # ℓ
    '\u2115': 'N',  # ℕ (Double-Struck N)
    '\u2119': 'P',  # ℙ (Double-Struck P)
    '\u211A': 'Q',  # ℚ (Double-Struck Q)
    '\u211B': 'R',  # ℛ (Script Capital R)
    '\u211C': 'R',  # ℜ (Fraktur Capital R)
    '\u211D': 'R',  # ℝ (Double-Struck R)
    '\u2124': 'Z',  # ℤ (Double-Struck Z)
    '\u2128': 'Z',  # ℨ (Fraktur Capital Z)
    '\u212C': 'B',  # ℬ (Script Capital B)
    '\u212D': 'C',  # ℭ (Fraktur Capital C)
    '\u212F': 'e',  # ℯ (Script small e)
    '\u2130': 'E',  # ℰ (Script Capital E)
    '\u2131': 'F',  # ℱ (Script Capital F)
    '\u2133': 'M',  # ℳ (Script Capital M)
    '\u2134': 'o',  # ℴ (Script small o)
    '\u2139': 'i',  # ℹ

    # Special: € used as 'e' in DB
    '\u20AC': 'e',  # €

    # ── Armenian additional ───────────────────────────────────────────────
    '\u054A': 'P',  # Պ → P

    # ── Latin Extended-A common diacritics ────────────────────────────────
    # (FormKD fails under InvariantGlobalization)
    '\u010E': 'D',  # Ď
    '\u010F': 'd',  # ď
    '\u0164': 'T',  # Ť
    '\u0165': 't',  # ť
    '\u0174': 'W',  # Ŵ
    '\u0175': 'w',  # ŵ
    '\u0159': 'r',  # ř
    '\u0158': 'R',  # Ř
    '\u0155': 'r',  # ŕ
    '\u0154': 'R',  # Ŕ
    '\u0117': 'e',  # ė
    '\u0116': 'E',  # Ė
    '\u015F': 's',  # ş
    '\u015E': 'S',  # Ş
    '\u011F': 'g',  # ğ
    '\u011E': 'G',  # Ğ
    '\u0148': 'n',  # ň
    '\u0147': 'N',  # Ň
    '\u013E': 'l',  # ľ
    '\u013D': 'L',  # Ľ
    '\u017E': 'z',  # ž
    '\u017D': 'Z',  # Ž
    '\u0161': 's',  # š
    '\u0160': 'S',  # Š
    '\u010D': 'c',  # č
    '\u010C': 'C',  # Č
    '\u0144': 'n',  # ń
    '\u0143': 'N',  # Ń
    '\u024F': 'y',  # ɏ
    '\u0166': 'T',  # Ŧ
    '\u0167': 't',  # ŧ
    '\u0126': 'H',  # Ħ
}


# ── Leet-Speak Mappings ────────────────────────────────────────────────────
# digit/symbol → letter
LEET_MAP: dict[str, str] = {
    '@': 'a',
    '$': 's',
    '0': 'o',
    '3': 'e',
    '1': 'l',
    '4': 'a',
    '5': 's',
    '6': 'b',
    '7': 't',
    '!': 'i',
    '9': 'g',
}


# ── Ambiguous character pairs for dictionary-based post-correction ─────────
# Ambiguous pairs for dictionary correction
AMBIGUOUS_PAIRS: list[tuple[str, str]] = [
    ('l', 'i'), ('i', 'l'),  # l ↔ i
    ('I', 'l'), ('v', 'u'),  # I → l, v → u
    ('u', 'v'),              # u → v
    ('b', 'g'), ('g', 'b'),  # b ↔ g (upside-down ambiguity)
]


# ── Word dictionary for ambiguity resolution ──────────────────────────────
WORD_DICTIONARY: set[str] = {
    # Common English words (500+)
    *"a an the and or but in on at to for of with by from is it its not no yes be am are was were been has have had do does did will would shall should can could may might must get got let set put run say see go come give take make like love know want need find look feel think back good bad big small hot cold new old first last long short hard easy best more most very much just only even still also too than then when where how who what which this that here there now up down out over off into about between through again another both each other such being having going coming making taking getting doing saying looking feeling thinking after before during while because never always every some any really around under inside outside above below near next still away right left sure thing way much many well just already quite really".split(),
    # Pronouns, prepositions, conjunctions
    *"her his she he him they them their your my our its me us you we who whom whose what which that these those i my mine myself we our ours ourselves you your yours yourself yourselves he him his himself she her hers herself it its itself they them their theirs themselves".split(),
    # Common adjectives/adverbs used in titles
    *"little pretty beautiful gorgeous stunning amazing perfect special private secret exclusive premium young teen mature old new blonde brunette redhead ebony latina asian japanese black white pink blue red wild crazy dirty nasty naughty horny wet tight slim thick curvy busty petite tall skinny tiny huge giant massive enormous giant small large big great super ultra mega extra intense extreme deep full open close free easy real hard soft long quick slow fast rough gentle sweet lovely cute adorable innocent guilty lucky happy ready willing eager forbidden taboo unexpected surprised caught cheating lazy sleepy drunk sober naked nude topless dressed undressed covered oiled tattooed pierced shaved smooth hairy natural fake enhanced silicone athletic fit toned muscular skinny chubby plump bbw thicc juicy perky round flat firm bouncy floppy saggy swollen puffy".split(),
    # Verbs common in titles
    *"fuck fucked fucking fucks suck sucked sucking sucks lick licked licking licks kiss kissed kissing ride riding rode rides bang banged banging blow blew blowing cum came coming cums swallow swallowed swallowing squirt squirted squirting gape gaped gaping choke choked choking spank spanked spanking tie tied tying bind bound flash flashed flashing strip stripped stripping tease teased teasing seduce seduced seducing surprise surprised surprising catch caught catching cheat cheated cheating share shared sharing swap swapped swapping watch watched watching film filmed filming record recorded recording help helped helping teach taught teaching learn learned learning train trained training punish punished punishing reward rewarded rewarding serve served serving clean cleaned cleaning cook cooked cooking deliver delivered delivering fix fixed fixing massage massaged massaging shower showered showering bathe bathed bathing swim swimming exercise exercised exercising stretch stretched stretching bend bending spread spreading squeeze squeezed squeezing thrust thrusting pound pounding hammer hammered hammering drill drilled drilling slam slammed slamming smash smashed smashing wreck wrecked wrecking destroy destroyed destroying dominate dominated dominating submit submitted submitting obey obeyed obeying beg begged begging plead pleaded please pleased pleasing satisfy satisfied satisfying crave craved craving desire desired desiring worship worshipped worshipping adore adored".split(),
    # Nouns — people, places, things in titles
    *"girl girls boy boys woman women man men lady ladies guy guys babe babes chick doll model actress pornstar performer star queen princess goddess angel devil demon slut whore bitch mistress master slave maid butler driver pilot captain soldier officer guard warden inmate prisoner cop police detective agent spy thief robber burglar pirate ninja samurai knight prince king emperor gladiator warrior fighter boxer wrestler athlete coach trainer instructor professor tutor mentor boss manager director ceo secretary assistant intern employee worker colleague coworker client customer patient landlord tenant neighbor stranger guest visitor tourist traveler hiker camper swimmer diver surfer skater dancer singer actress musician artist painter photographer reporter journalist writer author poet".split(),
    *"mom mommy mother mama dad daddy father papa son daughter sister brother aunt uncle cousin nephew niece grandmother grandfather grandma grandpa granny wife husband bride groom fiancee boyfriend girlfriend lover partner friend bestfriend roommate classmate teammate babysitter nanny tutor stepmother stepfather stepmom stepdad stepsister stepbrother stepdaughter stepson".split(),
    *"house home room bedroom bathroom kitchen living dining garage attic basement closet office studio apartment penthouse mansion villa cottage cabin lodge hotel motel resort spa gym pool jacuzzi sauna shower bath tub couch sofa bed mattress desk table chair floor wall door window balcony patio garden yard backyard rooftop beach island forest mountain lake river ocean park street alley parking car bus train plane boat yacht helicopter elevator staircase hallway lobby reception waiting dressing locker changing fitting prison cell dungeon castle tower church temple shrine library museum gallery theater cinema stadium arena ring cage bar club pub restaurant cafe diner shop store mall market salon barbershop tattoo parlor massage".split(),
    # Adult content vocabulary
    *"milf gilf dilf cougar pawg bbc bwc bbw ssbbw pov vr virtual reality threesome foursome fivesome gangbang bukkake orgy creampie facial cumshot blowjob handjob footjob rimjob titjob assjob throatpie internal external anal oral vaginal double triple penetration dp dvp dap airtight deepthroat gagging choking edging orgasm climax finish compilation montage pmv hmv converted remaster remastered fisting pegging strapon dildo vibrator toy toys fleshlight onahole insertion gaping prolapse enema squirting pissing watersports golden bondage bdsm shibari rope chain handcuff blindfold gag ball collar leash whip flogger paddle crop candle wax clamp clothespin cage chastity femdom maledom switch dominant submissive slave pet play cosplay roleplay fantasy scenario uniform costume outfit dress suit armor bikini lingerie underwear panties bra thong stockings fishnets heels boots gloves mask wig".split(),
    # Body parts and descriptors
    *"ass butt booty pussy cunt vagina cock dick penis tits boobs breasts chest nipples clit clitoris lips mouth tongue throat neck shoulders arms hands fingers nails feet toes legs thighs hips waist belly stomach abs core back spine ribs pelvis groin hole holes balls testicles shaft tip head skin hair bush landing strip tattoo tattoos piercing piercings scar birthmark freckle mole dimple muscle muscles bone bones joint joints vein veins nerve nerves".split(),
    # Common title structure words
    *"part episode scene chapter vol volume season series number special edition version extended full complete total uncut uncensored raw director cut behind scenes making exclusive debut premiere release return encore final ultimate best greatest collection compilation mix set bundle pack remastered remaster converted ai upscale enhanced improved restored".split(),
    # Common misspellings and variations
    *"luv cum cumming orgasming pleasuring servicing worshiping".split(),
    # Additional common English words that frequently appear in titles
    *"service services surprise surprised surprising birthday christmas halloween valentine valentines easter morning afternoon evening night midnight weekend holiday honeymoon anniversary wedding engagement date dinner lunch breakfast coffee drinks party club meeting appointment business trip travel adventure mission quest challenge game games play playing player luck lucky fortune golden silver diamond crystal angel devil fire flame ice snow rain storm thunder lightning dream dreams nightmare sleep sleeping awake waking morning routine daily weekly monthly annual professional amateur beginner expert master class lesson tutorial guide show performance stage concert live stream recording session practice rehearsal preparation celebration ceremony ritual tradition culture history story stories tale legend myth adventure journey road path trail expedition discovery exploration hunt treasure search rescue escape prison break breakout freedom liberation revenge justice truth dare bet wager challenge competition contest race fight battle war peace".split(),
    # More occupation/role words
    *"service servant waitress waiter bartender chef baker florist gardener cleaner janitor mechanic electrician plumber carpenter painter decorator designer architect engineer scientist researcher professor librarian accountant lawyer judge attorney prosecutor detective investigator analyst consultant advisor counselor therapist psychiatrist psychologist surgeon specialist technician assistant receptionist operator dispatcher coordinator supervisor inspector examiner auditor pharmacist veterinarian dentist orthodontist optometrist chiropractor physiotherapist acupuncturist masseuse masseur barber hairdresser stylist makeup artist photographer videographer cinematographer director producer editor writer journalist blogger influencer streamer gamer programmer developer hacker".split(),
    # Clothing and appearance
    *"clothing clothes dress dressed dressing shirt blouse top skirt pants jeans shorts leggings tights pantyhose underwear panties bra thong gstring corset bustier bodysuit jumpsuit romper robe bathrobe towel apron mask glasses sunglasses hat cap helmet crown tiara veil scarf gloves mittens belt buckle zipper button lace silk satin leather latex rubber vinyl mesh sheer transparent opaque tight loose fitted baggy torn ripped vintage retro modern classy elegant casual formal professional sporty athletic military".split(),
    # Actions and descriptors commonly in titles
    *"caught cheating secretly hidden camera spy voyeur peeping watching observing discovered exposed revealed confessed admitted betrayed forgiven punished rewarded testing tested trying tried failing failed passing passed winning losing playing pretending faking lying telling truth truth dare daring challenged accepted declined rejected refused invited welcomed received greeted approached introduced presented offered given taken stolen borrowed returned exchanged traded sold bought paid hired fired promoted demoted transferred assigned".split(),
}
