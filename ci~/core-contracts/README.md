# Protocol v1 contracts

This directory is the canonical source of the control-plane protocol v1 contract (yes2games/yes2sdk-core#42). Unity, Defold and the dashboard vendor exact copies of it. Nothing in `src/` imports it, so it never reaches the runtime bundle.

## Layout

| Path | Holds |
|---|---|
| `protocol/` | Wire schemas and the error set: `bootstrap-v1.schema.json`, `runtime-snapshot-v1.schema.json`, `errors-v1.json` |
| `modules/` | Module payload schemas: `config-v1.schema.json`, `liveops-event-v1.schema.json` |
| `fixtures/valid/` | Inputs that must be accepted |
| `fixtures/invalid/` | Inputs that must be rejected |
| `CONTRACTS.json` | Generated digest of every other file in this directory |

The `.gitkeep` files only hold the empty directories in Git. Delete one when its directory gets a real file.

Every file here is byte-exact. `.gitattributes` marks `contracts/**` as `-text`, so Git never converts line endings, and Prettier and ESLint do not touch this directory.

## Schemas

Schemas use JSON Schema draft 2020-12 and must declare `"$schema": "https://json-schema.org/draft/2020-12/schema"`. The test runner loads every `*.schema.json` file in `protocol/` and `modules/` with Ajv in strict mode, so an unknown keyword or another draft fails CI. A fixture names its schema by the schema's path from this directory, for example `protocol/bootstrap-v1.schema.json`.

Some rules in #42 cannot be written in JSON Schema: duplicate JSON keys, sizes in UTF-8 bytes, sort order, `artifactRefs` membership and the strict `Z` timestamp. Those land with the schema they belong to, as validator rules next to the runner.

## Fixtures

A fixture is a pair of files with the same name, in `fixtures/valid/` or `fixtures/invalid/`, in any subdirectory:

- `<name>.json` holds the exact input bytes. It does not have to be valid JSON or valid UTF-8.
- `<name>.expected.json` holds the one expected outcome: `{"schema": "<path of the schema>", "outcome": "<outcome>"}`.

The outcome is `ok` for every fixture in `valid/`. In `invalid/` it is the error category the input must produce, from `protocol/errors-v1.json`. The runner fails an outcome that is not a code in that file.

A schema that binds its document to the client's context needs `binding` in every expected outcome: an object with exactly the bound fields and the values the client expects. The input is invalid when one of those top-level fields differs. A bootstrap binds `gameKey` and `environmentKey`, the Game and Environment the client asked for. A runtime snapshot binds the bootstrap that points to it: `gameKey`, `releaseId` and `snapshotDigest`, which must equal `sha256:` and the hex SHA-256 of the exact input bytes.

A fixture in `valid/` may also list `reads`, the RFC 6901 JSON Pointer reads its input must answer. See [Pointer reads](#pointer-reads).

### Strict JSON

The runner applies these rules to every input, under every schema, before the schema:

- The bytes are UTF-8. A byte order mark is invalid.
- The bytes are exactly one JSON value, with optional JSON whitespace around it. Comments, trailing commas, `NaN` and other extensions are invalid.
- Keys are unique in each object, compared after unescaping.
- Every number is a finite IEEE-754 binary64 value. A literal that rounds to infinity, such as `1e309`, is invalid.
- Nesting depth is at most 64. The root is depth 1, and each nested array or object adds 1.

A string may hold an unpaired surrogate escape such as `"\ud800"` unless its schema says otherwise. config.v1 and the liveops.event.v1 `payload` do.

`__tests__/unit/contracts/fixtures.test.ts` runs the corpus in Jest. It fails when an input is rejected but expects `ok`, when an input is accepted but expects an error, when an outcome sits in the wrong directory, when an input has no expected outcome or an outcome has no input, or when an expected outcome names an unknown schema. An empty corpus passes.

Fixtures above the 512 KiB pre-commit limit are generated inside that test instead of committed. They are the 1048576-byte and 1048577-byte bootstrap and runtime snapshot. Each pads an otherwise valid document with an additive `padding` string.

## errors-v1.json

`protocol/errors-v1.json` lists the 14 error categories of #42 §9 in order. Each has a `code` and the `conditions` that produce it. A client reports exactly one code per failure and keeps native exceptions and HTTP details as internal diagnostics.

## bootstrap.v1

`protocol/bootstrap-v1.schema.json` describes the bootstrap document (#42 §2 and §3). Its fixtures are in `fixtures/valid/bootstrap-v1/` and `fixtures/invalid/bootstrap-v1/`. A client checks a bootstrap in this order, and the first failure decides the code:

1. At most 1048576 bytes, then the strict JSON rules. A failure is `bootstrap_invalid`.
2. The root is an object whose `protocolVersion` is a safe integer other than `1`: `protocol_unsupported`. Nothing else is checked, so a newer protocol may change every other field.
3. The schema and the rules below. A failure is `bootstrap_invalid`.
4. `gameKey` and `environmentKey` equal the Game and Environment the client asked for. A mismatch is `bootstrap_invalid`.

| Rule | Enforced by |
|---|---|
| All seven fields are required | schema |
| `protocolVersion` is `1`. An integral double such as `1.0` or `1e0` is `1`. A string, fraction, boolean, `null` or unsafe integer is invalid, not unsupported | schema, and the runner for unsupported versions |
| `gameKey` matches `^[a-z0-9][a-z0-9-]{0,62}$` and `environmentKey` matches `^[a-z0-9][a-z0-9._-]{0,63}$`. `$` is the end of the string: a trailing line feed is invalid | schema |
| `deploymentId` and `releaseId` are 1 to 128 ASCII characters, so 1 to 128 bytes. Any ASCII character counts, including spaces and control characters | schema |
| `snapshotUrl` starts with `https://` in any letter case, has a host, and holds only printable ASCII with no spaces | schema |
| `snapshotUrl` parses as a WHATWG URL with scheme `https`. This rejects an empty host or a port above 65535 | validator |
| `snapshotDigest` matches `^sha256:[0-9a-f]{64}$` | schema |
| Additive fields are ignored, at any nesting, but still follow the strict JSON rules | schema (no `additionalProperties`), runner |
| Binding to the requested Game and Environment | runner, from `binding` |

## runtime-snapshot.v1

`protocol/runtime-snapshot-v1.schema.json` describes the runtime snapshot (#42 §4 to §6). Its fixtures are in `fixtures/valid/runtime-snapshot-v1/` and `fixtures/invalid/runtime-snapshot-v1/`. A client checks a runtime snapshot in this order, and the first failure decides the code:

1. At most 1048576 bytes. A failure is `snapshot_invalid`.
2. The SHA-256 of the exact bytes equals the bootstrap `snapshotDigest`, before parsing. A mismatch is `snapshot_invalid`.
3. The strict JSON rules. A failure is `snapshot_invalid`.
4. The root is an object whose `protocolVersion` is a safe integer other than `1`: `protocol_unsupported`.
5. The schema and the rules below. A failure is `snapshot_invalid`.
6. `gameKey` and `releaseId` equal the bootstrap's. A mismatch is `snapshot_invalid`.

| Rule | Enforced by |
|---|---|
| The five top-level fields and the four fields of each Resource and Artifact are required. `data` may be any JSON value | schema |
| `gameKey`, `releaseId`, `protocolVersion` as in the bootstrap | schema |
| Resource `key` matches `^[a-z0-9][a-z0-9._-]{0,127}$`, `typeKey` matches `^[a-z0-9][a-z0-9._-]{0,63}$`, and `revisionId` is 1 to 128 ASCII characters | schema |
| Resource keys are unique and ascending, compared byte by byte | validator |
| Artifact `digest` matches `^sha256:[0-9a-f]{64}$`. Digests are unique and ascending, compared byte by byte | schema, validator |
| Artifact `mediaType` is a lowercase RFC 6838 `type/subtype` with no parameters: `^[a-z0-9][a-z0-9!#$&^_.+-]{0,126}/[a-z0-9][a-z0-9!#$&^_.+-]{0,126}$` | schema |
| Artifact `size` is an integer from 0 to 16777216. An integral double such as `1234.0` counts | schema |
| Artifact `url` follows the `snapshotUrl` rules | schema, validator |
| A Resource whose `typeKey` is known must have a valid payload. The known `typeKey`s are `config.v1` and `liveops.event.v1`. Its `data` must pass `modules/config-v1.schema.json` or `modules/liveops-event-v1.schema.json` and its validator rules, and the exact bytes of `data` in the snapshot, from its first to its last character, count toward the 131072-byte limit | validator |
| Every `artifactRefs` digest of a `liveops.event.v1` Resource is the `digest` of an Artifact in the same snapshot | validator |
| A Resource whose `typeKey` is unknown keeps `data` as opaque JSON. Only the strict JSON rules apply, so it never fails the snapshot on its own. Reading it through a module reports `resource_unsupported` | validator |
| Additive fields on the snapshot, a Resource or an Artifact are ignored | schema (no `additionalProperties`), runner |

## config.v1

`modules/config-v1.schema.json` describes the `data` of a `config.v1` Resource (#42 §6 and §7). Its fixtures are in `fixtures/valid/config-v1/` and `fixtures/invalid/config-v1/`. An invalid payload produces `snapshot_invalid`, because #42 §5 maps an invalid payload of a known `typeKey` to it.

The schema and the reference validator in `__tests__/unit/contracts/fixtures.test.ts` split the rules:

| Rule | Enforced by |
|---|---|
| The payload is a JSON object | schema |
| Values are boolean, number, string, array or object. `null` is invalid at any depth | schema |
| At most 1024 properties in each object | schema (`maxProperties`) |
| At most 1024 items in each array | schema (`maxItems`) |
| Nesting depth at most 16. The payload object is depth 1, and each nested array or object adds 1 | validator |
| Keys at most 128 UTF-8 bytes, after unescaping | validator. The schema's `maxLength: 128` counts code points, so it only catches part of this. |
| Strings at most 65536 UTF-8 bytes, after unescaping | validator. The schema's `maxLength: 65536` counts code points, so it only catches part of this. |
| Keys and strings are well-formed Unicode: an unpaired surrogate escape such as `"\ud800"` is invalid | validator |
| Numbers are finite IEEE-754 binary64 values. A literal that rounds to infinity, such as `1e309`, is invalid. Any finite value is valid, including integers beyond 2^53 | validator |
| Keys are unique in each object | validator, for every schema |
| The payload is at most 131072 bytes, counted on the exact input bytes | validator |

Safe integers matter only to integer reads. See `integer` below.

### Pointer reads

`reads` is an array. Each entry has a `pointer` and exactly one of these:

- `value`: the pointer resolves to this JSON value.
- `result: "absent"`: the pointer is valid RFC 6901, but nothing is at that location. A typed read returns the caller's default.
- `result: "malformed"`: the pointer is not valid RFC 6901. Either it is not empty and does not start with `/`, or a `~` is not followed by `0` or `1`. This is a caller error, not an error category from `errors-v1.json`.

An entry with a number `value` may add `integer`. It is `true` when an integer read accepts the value. That holds only when the binary64 value is integral and within `[-9007199254740991, 9007199254740991]`, so `1.0` and `1e3` qualify, and `1.5` and `9007199254740992` do not.

Resolution follows RFC 6901:

- The empty pointer is the whole payload.
- Reference tokens decode `~1` to `/` before `~0` to `~`.
- An object token matches an own key exactly, code unit for code unit, with no Unicode normalization.
- An array token is `0` or a decimal without a leading zero, below the array length. `-`, `01`, `+1` and an out-of-range index are absent.
- A token applied to a string, number or boolean is absent.

## CONTRACTS.json

After any change in this directory, run `npm run contracts:generate` and commit the result. The Jest test `__tests__/unit/scripts/generate-contracts.test.ts` runs `node scripts/generate-contracts.mjs --check`, so a changed, added or removed byte without a regenerated `CONTRACTS.json` fails CI.

`CONTRACTS.json` holds:

- `schemaVersion`: `1`.
- `files`: every file in this directory except `CONTRACTS.json`, each with its path from this directory and `sha256:<hex>` of its exact bytes, sorted by path in byte order. Paths use only `A-Z a-z 0-9 . _ - /`.
- `aggregate`: `sha256:<hex>` of the `sha256sum` listing of those files, in that order. Each line is the lowercase hex digest, two spaces, the path and a line feed.

Any language can recompute the aggregate. From this directory, the shell form is:

```sh
find . -type f ! -path ./CONTRACTS.json | sed 's|^\./||' | LC_ALL=C sort | xargs sha256sum | sha256sum
```

## Vendoring

A downstream repository copies this directory unchanged and commits a lock next to it. The lock records the Core commit or tag the copy came from and the `aggregate` of that copy. Its CI recomputes the aggregate over the vendored files, compares it with the lock and with the vendored `CONTRACTS.json`, and needs no access to Core. A contract change in Core reaches a downstream repository only through a deliberate vendor update.

## liveops.event.v1

`modules/liveops-event-v1.schema.json` describes the `data` of a `liveops.event.v1` Resource (#42 §8). Its fixtures are in `fixtures/valid/liveops-event-v1/` and `fixtures/invalid/liveops-event-v1/`, and the snapshot fixtures named `liveops-*` cover the rules that need the whole snapshot. An invalid payload produces `snapshot_invalid`, as for config.v1.

| Rule | Enforced by |
|---|---|
| The payload is a JSON object with exactly `startAt`, `endAt`, `payload` and `artifactRefs`. An additive field is invalid | schema |
| `startAt` and `endAt` match `^(?!0000)[0-9]{4}-(?:0[1-9]\|1[0-2])-(?:0[1-9]\|[12][0-9]\|3[01])T(?:[01][0-9]\|2[0-3]):[0-5][0-9]:[0-5][0-9](?:\.[0-9]{1,3})?Z$`: RFC 3339 in UTC with an uppercase `T` and `Z`, years 0001 to 9999, no leap second and 0 to 3 fraction digits. `[0-9]` is ASCII only, and `$` is the end of the string | schema |
| The date exists in the proleptic Gregorian calendar: `2027-02-29` and `2026-04-31` are invalid | validator |
| `startAt` is before `endAt`, compared as instants. `00:00:00.5Z` and `00:00:00.500Z` are the same instant, and `00:00:00Z` is before `00:00:00.001Z`, so compare parsed values, not strings | validator |
| `payload` is a config.v1 payload: the config.v1 schema and validator rules apply to it, with `payload` itself at depth 1 | schema, validator |
| `artifactRefs` holds at most 64 unique strings matching `^sha256:[0-9a-f]{64}$`, in any order | schema |
| Every `artifactRefs` digest is an Artifact `digest` in the same snapshot | validator, in the runtime snapshot |
| The payload is at most 131072 bytes, counted on the exact input bytes | validator |

An event is active at instant `t` when `startAt <= t < endAt`. It starts active at `startAt` and is no longer active at `endAt`. Its identity is the enclosing Resource `key` and `revisionId`, and a client lists events in ascending Resource key order.
