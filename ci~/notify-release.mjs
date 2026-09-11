#!/usr/bin/env node
// notify-release.mjs - the published-stable-only Google Chat notifier
// (yes2games/yes2sdk-unity#97 section 11).
//
// Node and nothing else. This organization's runners are self-hosted and supply
// neither gh nor jq, and the notifier is a read-only consumer that should not be
// a reason to install a toolchain: Node 22 has fetch, so the release read and the
// webhook post both go through it.
//
// The release is read back from the API rather than taken from the event payload,
// because this workflow has two triggers and they carry different shapes. A
// Release published with GITHUB_TOKEN fires no `release: published` event, so the
// release job wakes this workflow with an explicit repository_dispatch; the
// `release` trigger still covers a release published by a person through the UI.
// One lookup means one code path and no payload-shaped bugs in the rarer one.
//
// Usage: node ci~/notify-release.mjs <tag>
//
// Environment: GITHUB_TOKEN, GITHUB_REPOSITORY, GCHAT_WEBHOOK_URL.
//
// Exit codes: 0 sent or deliberately skipped, 1 failure.

const [tag] = process.argv.slice(2);
const { GITHUB_TOKEN, GITHUB_REPOSITORY, GCHAT_WEBHOOK_URL } = process.env;

function die(message) {
    process.stderr.write(`notify-release: ${message}\n`);
    process.exit(1);
}

if (!tag) {
    die("no tag given; neither the release event nor the dispatch payload carried one");
}
for (const [name, value] of Object.entries({ GITHUB_TOKEN, GITHUB_REPOSITORY, GCHAT_WEBHOOK_URL })) {
    if (!value) {
        die(`${name} is not set`);
    }
}

const response = await fetch(
    `https://api.github.com/repos/${GITHUB_REPOSITORY}/releases/tags/${encodeURIComponent(tag)}`,
    {
        headers: {
            accept: "application/vnd.github+json",
            authorization: `Bearer ${GITHUB_TOKEN}`,
            "x-github-api-version": "2022-11-28",
        },
    }
);
if (!response.ok) {
    die(`cannot read release ${tag}: HTTP ${response.status} ${response.statusText}`);
}
const release = await response.json();

// Published stable only. A draft has no business being announced, and this
// repository publishes no prereleases at all - edge is a Git tag with no Release
// behind it - so one reaching here is a contract violation, not a notification.
if (release.draft || release.prerelease) {
    process.stdout.write(`${tag} is a draft or a prerelease; this notifier is published-stable-only\n`);
    process.exit(0);
}

const body = (release.body ?? "").slice(0, 1500) || "_(no release notes)_";

const card = {
    cardsV2: [
        {
            cardId: "release-card",
            card: {
                header: {
                    title: `New Release ${release.tag_name}`,
                    subtitle: GITHUB_REPOSITORY,
                    imageUrl: "https://github.githubassets.com/favicons/favicon.png",
                    imageType: "CIRCLE",
                },
                sections: [
                    {
                        widgets: [
                            { decoratedText: { topLabel: "Release", text: release.name || release.tag_name } },
                            { decoratedText: { topLabel: "Status", text: "Stable Release" } },
                            { decoratedText: { topLabel: "Author", text: release.author?.login ?? "unknown" } },
                            { textParagraph: { text: body } },
                            {
                                buttonList: {
                                    buttons: [
                                        { text: "View on GitHub", onClick: { openLink: { url: release.html_url } } },
                                    ],
                                },
                            },
                        ],
                    },
                ],
            },
        },
    ],
};

const posted = await fetch(GCHAT_WEBHOOK_URL, {
    method: "POST",
    headers: { "content-type": "application/json; charset=UTF-8" },
    body: JSON.stringify(card),
});
if (!posted.ok) {
    die(`webhook rejected the card: HTTP ${posted.status} ${posted.statusText}\n${await posted.text()}`);
}

process.stdout.write(`notified Google Chat about ${release.tag_name}\n`);
