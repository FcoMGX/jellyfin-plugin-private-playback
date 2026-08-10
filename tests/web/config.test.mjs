import assert from 'node:assert/strict';
import {
    loadStrings,
    selectCulture
} from '../../src/Jellyfin.Plugin.PrivatePlayback/Configuration/Web/config.js';

globalThis.document = { documentElement: { lang: 'en-GB' } };

const cultureCases = [
    ['es-ES', 'es-ES'],
    ['pt_PT', 'pt-PT'],
    ['zh-Hant-TW', 'zh-TW'],
    ['ja', 'ja-JP'],
    ['en-US', 'en-GB'],
    ['', 'en-GB'],
    ['unsupported', 'en-GB']
];

for (const [requested, expected] of cultureCases) {
    document.documentElement.lang = requested;
    assert.equal(selectCulture(), expected);
}

const requests = [];
globalThis.ApiClient = {
    getUrl(path, query) {
        return `${path}?name=${query.name}`;
    },
    async ajax(request) {
        requests.push(request.url);
        if (request.url.includes('es-ES')) {
            throw new Error('simulated missing locale');
        }

        if (request.url.includes('pt-PT')) {
            return { Title: '', Save: 'Guardar' };
        }

        return { Title: 'Private Playback', Save: 'Save' };
    }
};
document.documentElement.lang = 'es-ES';
const strings = await loadStrings();
assert.equal(strings.Title, 'Private Playback');
assert.deepEqual(requests, [
    'web/ConfigurationPage?name=PrivatePlayback.i18n.es-ES.json',
    'web/ConfigurationPage?name=PrivatePlayback.i18n.en-GB.json'
]);

requests.length = 0;
document.documentElement.lang = 'pt-PT';
const incompleteFallback = await loadStrings();
assert.deepEqual(incompleteFallback, { Title: 'Private Playback', Save: 'Save' });
assert.deepEqual(requests, [
    'web/ConfigurationPage?name=PrivatePlayback.i18n.pt-PT.json',
    'web/ConfigurationPage?name=PrivatePlayback.i18n.en-GB.json'
]);

process.stdout.write('Web localisation tests passed.\n');
