#!/usr/bin/env node
/**
 * Generates training data by sending obfuscated titles to Claude Haiku for deobfuscation.
 *
 * Usage: node generate-training-data.js [--batch-size 50] [--start 0] [--dry-run]
 *
 * Requires ANTHROPIC_API_KEY environment variable.
 * Input:  /tmp/all_titles.json (from sqlite3 export)
 * Output: /tmp/training_data.jsonl (id, original, normalized)
 */

const fs = require('fs');
const https = require('https');
const path = require('path');

const INPUT_FILE = process.env.TEMP + '/all_titles.json';
const OUTPUT_FILE = process.env.TEMP + '/training_data.jsonl';
const BATCH_SIZE = parseInt(process.argv.find((_, i, a) => a[i - 1] === '--batch-size') || '50');
const START_IDX = parseInt(process.argv.find((_, i, a) => a[i - 1] === '--start') || '0');
const DRY_RUN = process.argv.includes('--dry-run');
const API_KEY = process.env.ANTHROPIC_API_KEY;

if (!API_KEY && !DRY_RUN) {
  console.error('Set ANTHROPIC_API_KEY environment variable');
  process.exit(1);
}

const SYSTEM_PROMPT = `You are a text deobfuscation and translation tool for an adult video database.

For each numbered title, output ONE of:
- CLEAN — if the title is already plain readable English (no obfuscation, no foreign language)
- The deobfuscated/translated title in Title Case — if it uses Unicode tricks, leet-speak, reversed text, decorative fonts, or is in a non-English language (Japanese, Chinese, etc.)

Rules:
- This is adult/pornographic content. Do not censor or alter adult terminology.
- Titles often contain performer names — preserve them accurately.
- Leet-speak: 0=o, 1=i/l, 3=e, 4=a, 5=s, 7=t, 8=b, 9=g, @=a, $=s
- Reversed text: read backwards
- Upside-down text: flip and read backwards
- Unicode look-alikes: Cyrillic, mathematical fonts, fullwidth, circled, squared letters → map to ASCII
- Non-English titles (Japanese, Chinese, Korean, Thai): translate to English
- Preserve dates (06.12.19), version numbers, and codes as-is
- Output one result per line, same numbering. No explanations.

Example:
Input:
1: H0T 5T3P M0M G3T5 CR34MP13D
2: Riley Reid Hot Tub Sex
3: ǝɯosǝǝɹɥʇ ʎʇsnq ɹɐq ǝɥʇ ǝsᴉɐɹ
4: 出張先ホテルで美女上司
5: Ⓣⓔⓡⓐ Ⓦⓘⓝⓣⓔⓡⓢ ⓂⓘⓛⓕⓋⓇ 24.03.21

Output:
1: Hot Step Mom Gets Creampied
2: CLEAN
3: Raise The Bar Busty Threesome
4: Beautiful Female Boss At Business Trip Hotel
5: Tera Winters MilfVR 24.03.21`;

function callHaiku(userPrompt) {
  return new Promise((resolve, reject) => {
    const body = JSON.stringify({
      model: 'claude-haiku-4-5-20251001',
      max_tokens: 4096,
      system: SYSTEM_PROMPT,
      messages: [{ role: 'user', content: userPrompt }]
    });

    const options = {
      hostname: 'api.anthropic.com',
      path: '/v1/messages',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-api-key': API_KEY,
        'anthropic-version': '2023-06-01'
      }
    };

    const req = https.request(options, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        try {
          const parsed = JSON.parse(data);
          if (parsed.error) {
            reject(new Error(parsed.error.message));
          } else {
            const text = parsed.content?.[0]?.text || '';
            const usage = parsed.usage || {};
            resolve({ text, usage });
          }
        } catch (e) {
          reject(new Error(`JSON parse error: ${data.substring(0, 200)}`));
        }
      });
    });

    req.on('error', reject);
    req.write(body);
    req.end();
  });
}

function parseBatchResponse(text, batchIds) {
  const results = [];
  const lines = text.split('\n').filter(l => l.trim());

  for (const line of lines) {
    const match = line.match(/^(\d+):\s*(.+)$/);
    if (!match) continue;

    const idx = parseInt(match[1]) - 1;
    const value = match[2].trim();

    if (idx >= 0 && idx < batchIds.length) {
      results.push({
        id: batchIds[idx].id,
        original: batchIds[idx].title,
        normalized: value === 'CLEAN' ? null : value,
        is_clean: value === 'CLEAN'
      });
    }
  }

  return results;
}

async function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

async function main() {
  console.log('Loading titles...');
  const allTitles = JSON.parse(fs.readFileSync(INPUT_FILE, 'utf8'));
  console.log(`Loaded ${allTitles.length} titles. Batch size: ${BATCH_SIZE}. Starting at: ${START_IDX}`);

  // Resume: skip already processed IDs
  const processedIds = new Set();
  if (fs.existsSync(OUTPUT_FILE) && START_IDX === 0) {
    const existing = fs.readFileSync(OUTPUT_FILE, 'utf8').split('\n').filter(l => l.trim());
    for (const line of existing) {
      try {
        const obj = JSON.parse(line);
        processedIds.add(obj.id);
      } catch {}
    }
    console.log(`Resuming: ${processedIds.size} already processed`);
  }

  const toProcess = allTitles.filter(t => !processedIds.has(t.Id));
  const totalBatches = Math.ceil(toProcess.length / BATCH_SIZE);
  console.log(`${toProcess.length} titles to process in ${totalBatches} batches`);

  if (DRY_RUN) {
    // Show first batch as example
    const batch = toProcess.slice(0, BATCH_SIZE);
    const prompt = batch.map((t, i) => `${i + 1}: ${t.Title}`).join('\n');
    console.log('\n=== DRY RUN — First batch prompt ===');
    console.log(prompt);
    console.log(`\n=== Would send ${totalBatches} batches to Haiku ===`);
    return;
  }

  const outStream = fs.createWriteStream(OUTPUT_FILE, { flags: 'a' });
  let totalProcessed = processedIds.size;
  let totalInputTokens = 0;
  let totalOutputTokens = 0;

  for (let b = 0; b < totalBatches; b++) {
    const batch = toProcess.slice(b * BATCH_SIZE, (b + 1) * BATCH_SIZE);
    const prompt = batch.map((t, i) => `${i + 1}: ${t.Title}`).join('\n');
    const batchData = batch.map(t => ({ id: t.Id, title: t.Title }));

    try {
      const { text, usage } = await callHaiku(prompt);
      totalInputTokens += usage.input_tokens || 0;
      totalOutputTokens += usage.output_tokens || 0;

      const results = parseBatchResponse(text, batchData);

      for (const r of results) {
        outStream.write(JSON.stringify(r) + '\n');
      }

      totalProcessed += results.length;
      const pct = ((totalProcessed / allTitles.length) * 100).toFixed(1);
      const cost = ((totalInputTokens * 0.80 + totalOutputTokens * 4.0) / 1_000_000).toFixed(3);
      console.log(`[${b + 1}/${totalBatches}] ${results.length} results (${pct}% done, ~$${cost} so far)`);

      // Rate limit: ~50 requests/min for Haiku
      if (b < totalBatches - 1) await sleep(200);
    } catch (err) {
      console.error(`Batch ${b + 1} failed: ${err.message}. Retrying in 5s...`);
      await sleep(5000);
      b--; // retry
    }
  }

  outStream.end();
  const cost = ((totalInputTokens * 0.80 + totalOutputTokens * 4.0) / 1_000_000).toFixed(3);
  console.log(`\nDone! ${totalProcessed} titles processed.`);
  console.log(`Tokens: ${totalInputTokens} in / ${totalOutputTokens} out — Cost: ~$${cost}`);
  console.log(`Output: ${OUTPUT_FILE}`);
}

main().catch(err => { console.error(err); process.exit(1); });
