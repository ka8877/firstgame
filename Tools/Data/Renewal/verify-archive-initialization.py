#!/usr/bin/env python3
"""Prove bounded archive initialization preserves every pre-split literal row."""
import hashlib
import importlib.util
import json
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
LEDGER = HERE / 'archive-initialization-parity.json'
SOURCE = ROOT / 'Games/FantasyCards/Assets/Game/Core/OriginalArchiveData.cs'
GENERATOR = HERE / 'build-catalog.py'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def verify():
    ledger = json.loads(LEDGER.read_text())
    data = SOURCE.read_bytes()
    text = data.decode('utf-8')
    assert sha(data) == ledger['candidateSourceSha256'], 'Generated source changed'
    rows = re.findall(r'^   target\[(\d+)\] = (new OriginalArchiveCard\(.*\) \{ .* \});$', text, re.M)
    count = ledger['recordCount']
    assert len(rows) == count == 15350
    expressions = []
    sequence = hashlib.sha256()
    for expected, (raw_index, expression) in zip(ledger['records'], rows):
        index = int(raw_index)
        assert index == expected['index'] == len(expressions)
        payload = expression.encode('utf-8')
        assert len(payload) == expected['expressionUtf8Bytes']
        assert sha(payload) == expected['expressionSha256'], f'Literal changed at {index}'
        sequence.update(len(payload).to_bytes(4, 'little'))
        sequence.update(payload)
        expressions.append(expression)
    assert sequence.hexdigest() == ledger['originalExpressionSequenceSha256']
    assert sequence.hexdigest() == ledger['candidateExpressionSequenceSha256']
    assert re.search(r'var cards = new OriginalArchiveCard\[(\d+)\];', text).group(1) == str(count)
    calls = [int(n) for n in re.findall(r'^   FillChunk(\d{3})\(cards\);$', text, re.M)]
    assert calls == list(range((count + 127) // 128)) == ledger['orderedChunkCalls']
    attribute = '[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]'
    methods = re.findall(r'^  ' + re.escape(attribute) + r'\n  private static void FillChunk(\d{3})\(OriginalArchiveCard\[\] target\)\n  \{\n(.*?)^  \}', text, re.M | re.S)
    assert len(methods) == len(calls) == 120
    for chunk, (number, body) in enumerate(methods):
        assert int(number) == chunk
        indices = [int(n) for n in re.findall(r'^   target\[(\d+)\] = ', body, re.M)]
        assert indices == list(range(chunk * 128, min((chunk + 1) * 128, count)))
        assert len(indices) <= 128
    spec = importlib.util.spec_from_file_location('renewal_archive_generator', GENERATOR)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    assert module.render_archive(expressions).encode('utf-8') == data
    return {
        'status': 'passed',
        'records': count,
        'fieldsPreservedPerRecord': 17,
        'literalExpressionOrderAndBytesUnchanged': True,
        'absoluteIndicesContiguous': True,
        'noInliningChunks': len(methods),
        'maximumRowsPerChunk': 128,
        'lastChunkRows': len(rows) % 128,
        'expressionSequenceSha256': sequence.hexdigest(),
        'sourceSha256': sha(data),
        'generatorSha256': sha(GENERATOR.read_bytes()),
        'generatorReproducesOutput': True,
    }


if __name__ == '__main__':
    result = verify()
    (HERE / 'archive-initialization-verification.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result, indent=2))
