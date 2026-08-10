const pluginId = 'bb23ffd1-026a-4598-8133-e77ae50ccad7';
const supportedCultures = ['en-GB', 'es-ES', 'pt-PT', 'fr-FR', 'it-IT', 'zh-TW', 'ja-JP', 'ru-RU', 'ko-KR'];

export function selectCulture() {
    const requested = (document.documentElement.lang || 'en-GB').replace('_', '-').toLowerCase();
    const exact = supportedCultures.find(culture => culture.toLowerCase() === requested);
    if (exact) {
        return exact;
    }

    const language = requested.split('-')[0];
    return supportedCultures.find(culture => culture.toLowerCase().startsWith(language + '-')) || 'en-GB';
}

export async function loadStrings() {
    const culture = selectCulture();
    const fetchCulture = requestedCulture => ApiClient.ajax({
        url: ApiClient.getUrl('web/ConfigurationPage', {
            name: `PrivatePlayback.i18n.${requestedCulture}.json`
        }),
        type: 'GET',
        dataType: 'json'
    });
    try {
        const requestedStrings = await fetchCulture(culture);
        if (culture === 'en-GB') {
            return requestedStrings;
        }

        const fallbackStrings = await fetchCulture('en-GB');
        const fallbackKeys = Object.keys(fallbackStrings);
        const isComplete = fallbackKeys.length === Object.keys(requestedStrings).length
            && fallbackKeys.every(key => typeof requestedStrings[key] === 'string' && requestedStrings[key].trim() !== '');
        return isComplete ? requestedStrings : fallbackStrings;
    } catch (error) {
        if (culture === 'en-GB') {
            throw error;
        }

        return fetchCulture('en-GB');
    }
}

function localize(root, strings) {
    root.querySelectorAll('[data-i18n]').forEach(element => {
        const key = element.dataset.i18n;
        if (Object.hasOwn(strings, key)) {
            element.textContent = strings[key];
        }
    });
}

function modeDescription(mode, strings) {
    if (mode === 1) {
        return strings.PrivateDescription;
    }

    if (mode === 2) {
        return strings.CustomDescription;
    }

    return strings.NormalDescription;
}

function updateMode(card, strings) {
    const mode = Number(card.querySelector('[data-name="mode"]').value);
    card.querySelector('[data-name="customOptions"]').classList.toggle('hide', mode !== 2);
    card.querySelector('[data-name="modeDescription"]').textContent = modeDescription(mode, strings);
}

function configuredPolicy(configuration, userId) {
    return (configuration.UserPolicies || []).find(policy => policy.UserId.toLowerCase() === userId.toLowerCase());
}

function createUserCard(view, user, policy, strings, missing) {
    const fragment = view.querySelector('#privatePlaybackUserTemplate').content.cloneNode(true);
    const card = fragment.querySelector('.privatePlaybackUser');
    card.dataset.userId = user.Id;
    card.dataset.missing = String(missing);
    card.querySelector('[data-name="userName"]').textContent = user.Name;
    card.querySelector('[data-name="missingUser"]').classList.toggle('hide', !missing);
    const modeSelect = card.querySelector('[data-name="mode"]');
    const modeDescriptionElement = card.querySelector('[data-name="modeDescription"]');
    const idSuffix = user.Id.replaceAll('-', '');
    modeSelect.id = `privatePlaybackMode-${idSuffix}`;
    modeDescriptionElement.id = `privatePlaybackModeDescription-${idSuffix}`;
    modeSelect.setAttribute('aria-describedby', modeDescriptionElement.id);
    card.querySelector('[data-name="modeLabel"]').htmlFor = modeSelect.id;
    const mode = policy?.Mode ?? 0;
    modeSelect.value = String(mode);
    card.querySelector('[data-name="rememberProgress"]').checked = policy?.RememberProgress ?? true;
    card.querySelector('[data-name="rememberWatched"]').checked = policy?.RememberWatched ?? true;
    card.querySelector('[data-name="recordHistory"]').checked = policy?.RecordHistory ?? true;
    modeSelect.addEventListener('change', () => updateMode(card, strings));
    card.querySelector('[data-name="preview"]').addEventListener('click', () => previewCleanup(card, strings));
    card.querySelector('[data-name="clear"]').addEventListener('click', () => clearPlaybackData(card, strings));
    localize(card, strings);
    updateMode(card, strings);
    return fragment;
}

async function requestJson(url, method, body) {
    const request = {
        url: ApiClient.getUrl(url),
        type: method,
        dataType: 'json',
        headers: { accept: 'application/json' }
    };
    if (body !== undefined) {
        request.contentType = 'application/json';
        request.data = JSON.stringify(body);
        request.headers['content-type'] = 'application/json';
    }

    return ApiClient.ajax(request);
}

async function previewCleanup(card, strings) {
    const resultElement = card.querySelector('[data-name="cleanupResult"]');
    resultElement.textContent = strings.Loading;
    try {
        const result = await requestJson(
            `PrivatePlayback/Users/${encodeURIComponent(card.dataset.userId)}/PlaybackData/Preview`,
            'GET');
        resultElement.textContent = strings.PreviewResult.replace('{count}', result.AffectedItemCount);
    } catch (error) {
        resultElement.textContent = strings.CleanupError;
    }
}

async function clearPlaybackData(card, strings) {
    if (!window.confirm(strings.ConfirmPrompt)) {
        return;
    }

    const resultElement = card.querySelector('[data-name="cleanupResult"]');
    resultElement.textContent = strings.Loading;
    try {
        const result = await requestJson(
            `PrivatePlayback/Users/${encodeURIComponent(card.dataset.userId)}/PlaybackData/Clear`,
            'POST',
            { Confirmation: 'CLEAR_PLAYBACK_DATA' });
        resultElement.textContent = strings.ClearResult.replace('{count}', result.ClearedItemCount);
    } catch (error) {
        resultElement.textContent = strings.CleanupError;
    }
}

function readPolicies(view) {
    return Array.from(view.querySelectorAll('.privatePlaybackUser'))
        .filter(card => card.dataset.missing !== 'true' || Number(card.querySelector('[data-name="mode"]').value) !== 0)
        .map(card => ({
            UserId: card.dataset.userId,
            LastKnownName: card.querySelector('[data-name="userName"]').textContent,
            Mode: Number(card.querySelector('[data-name="mode"]').value),
            RememberProgress: card.querySelector('[data-name="rememberProgress"]').checked,
            RememberWatched: card.querySelector('[data-name="rememberWatched"]').checked,
            RecordHistory: card.querySelector('[data-name="recordHistory"]').checked
        }));
}

async function loadPage(view, strings) {
    Dashboard.showLoadingMsg();
    try {
        localize(view, strings);
        const [configuration, users, status] = await Promise.all([
            ApiClient.getPluginConfiguration(pluginId),
            ApiClient.getUsers(),
            requestJson('PrivatePlayback/Status', 'GET')
        ]);
        const container = view.querySelector('#privatePlaybackUsers');
        container.replaceChildren();
        const currentIds = new Set(users.map(user => user.Id.toLowerCase()));
        users.forEach(user => {
            container.appendChild(createUserCard(
                view,
                user,
                configuredPolicy(configuration, user.Id),
                strings,
                false));
        });
        (configuration.UserPolicies || [])
            .filter(policy => !currentIds.has(policy.UserId.toLowerCase()))
            .forEach(policy => {
                container.appendChild(createUserCard(
                    view,
                    { Id: policy.UserId, Name: policy.LastKnownName || policy.UserId },
                    policy,
                    strings,
                    true));
            });
        if (users.length === 0 && (configuration.UserPolicies || []).length === 0) {
            const empty = document.createElement('p');
            empty.textContent = strings.NoUsers;
            container.appendChild(empty);
        }

        view.querySelector('#privatePlaybackStatus').textContent = status.IsActive
            ? strings.EnforcementActive
            : strings.EnforcementInactive;
        view.querySelector('#privatePlaybackStatusReason').textContent =
            `${status.Reason} ${strings.ServerVersion}: ${status.ServerVersion}`;
        view.querySelectorAll('[data-name="cleanup"] button').forEach(button => {
            button.disabled = !status.IsActive;
        });
    } catch (error) {
        view.querySelector('#privatePlaybackStatus').textContent = strings.LoadError;
    } finally {
        Dashboard.hideLoadingMsg();
    }
}

export default function (view) {
    let strings;
    view.querySelector('#privatePlaybackConfigurationForm').addEventListener('submit', async event => {
        event.preventDefault();
        Dashboard.showLoadingMsg();
        try {
            const configuration = await ApiClient.getPluginConfiguration(pluginId);
            configuration.SchemaVersion = 1;
            configuration.UserPolicies = readPolicies(view);
            const result = await ApiClient.updatePluginConfiguration(pluginId, configuration);
            Dashboard.processPluginConfigurationUpdateResult(result);
        } catch (error) {
            Dashboard.hideLoadingMsg();
            Dashboard.alert(strings?.SaveError || 'Unable to save configuration.');
        }
    });

    view.addEventListener('viewshow', async () => {
        try {
            strings = await loadStrings();
            await loadPage(view, strings);
        } catch (error) {
            Dashboard.hideLoadingMsg();
        }
    });
}
