# Persistent Game Systems

### Note to contributors
Thank you for working on this system. Since there are so many moving parts try to follow these outlines and please keep this document up to date with any new or changed tables you make. That way any future contributors can keep this as a nice reference in the future.

----

# Overview

## Purpose
The fundamental unit of the game is the round. With few exceptions, each round is a completely fresh start. The story may develop through roleplay but the game does little to evolve alongside it. When the round ticks over, all evidence of the actions taken are lost beyond the memories of the players who observed them.

The goal of this project is to bridge the gap of continuity between rounds by introducing persistent systems. The first step is to introduce a record system that persists across rounds. With that groundwork set, we can introduce more systems which will build on that to provide a tangible impact on play. Meaning, actions taken in a given round will have a lasting impact on that character's, and other character's, future.

## Roadmap and Subsystems
Not all of these systems are currently planned and will need further discussion.

- [X] RecordCharacter
	- [X] RecordPersonalNote
	- [ ] RecordSLENote
	- [ ] RecordMedicalNote
	- [ ] RecordLicense
		- [ ] Persistent Licenses (`License` table)
	- [ ] RecordArrest
	- [ ] RecordLegalCase
	- [ ] RecordEmployment
	- [X] RecordEdit
- [ ] Admin Tooling
    - [X] Logging

### Persistent Licenses
Licenses may be granted or revoked in game by certain whitelisted roles. The status of a license is tracked in the `License` table and reflected in future rounds. A revoked license may mean spawning without the license or being unable to perform certain roles on that character until an appropriate license is granted.

---

# Design Decisions & Notes

### Naming Convention
All Aurora Song record tables use a `Record` prefix (e.g. `RecordCharacter`, `RecordSLENote`) so they sort together in database tooling and are clearly identifiable as part of this system. The `License` table is an exception as it is not a record but references to the collection of active licences themselves.

### Pattern
The design follows an **inheritance pattern**: a single `RecordCharacter` parent table holds data common to all record types, with a dedicated child table per record type containing type-specific fields. Each child table shares a 1-1 relationship with its parent, using `record_character_id` as both its primary key and foreign key.

### Time Display
`created_at` is stored as a real server timestamp. When displaying dates in-game, **add 500 to the year** in the display layer only. Do not store or calculate the offset in the database. Players can compare to the canonical time by referring to their PDA or consoles.

### Auditing
Changes to records should be recorded in admin logs for auditing purposes. A `RecordEdit` entry should be created for each in-game change to a record, which includes the record id, field changed, old value, and new value for easy querying.

Record changes can be audited by admins via the admin log system. For ease of use, the player id of the author is stored on the parent record as well. Author profile ids are used in-game to for cases such as allowing authors to modify their own records.

### Soft Deletes
Two soft delete flags exist with distinct semantics:

- `hidden` - In-character removal. The record is hidden from normal in-game display. All in-game actions that hide or restore a record should create a corresponding `RecordEdit` entry reflecting the hidden flag being set or unset. This should only be applied to records that are acceptable if shown to players either though admemes or specialized in game mechanisms.
- `deleted` - OOC/admin removal. Hidden from all in-game display except via admin tools. Tracked with `deleted_by_id` and `deleted_at` for accountability. The row is never physically removed from the database. These records account for records that should never be exposed in game and are effectively removed from in game history.

Physical deletion of rows should only be performed by database operators at the database level, and only for exceptional cases such as leaked PII or legal requirements. Prefer soft deletes in all other cases. Database operators are responsible for handling any orphaned entries manually.

### `target_character_id`
`target_character_id` indicates the character who the record pertains to. It is the primary way of indexing most records in-game. It references `profile.id` rather than a player account, as records belong to characters not accounts.

Profile rows are not deleted from the database, character deletion is handled by de-referencing that characters `profile` from that players `preference`. This ensures `target_character_id` always resolves and records are never orphaned. If a profile must be physically deleted, actions doing so should refer to **Soft Deletes** as for how to handle cascading deletions.

### `ProfileId`
`ProfileId` is held and accessed at the EntitySystem level via a field on `IdCardComponent`. This means that the ID card is the in-game representation of a character's stored profile.

### Querying
Records are queried from the parent down, filtering by `record_type` and `target_character_id`. A composite index on `(record_type, target_character_id, created_at DESC)` is defined on `RecordCharacter` for this access pattern. Child tables are joined via `Include()` on the navigation property. Queries always join the parent to ensure soft delete flags are respected.

### License Two-Table Pattern
Licenses use two tables:

- `License` - current state. One row per active license. Used for fast gameplay checks (e.g. does this character have a weapons license?). Row presence implies the license exists.
- `RecordLicense` - append-only history of license actions, parented to `RecordCharacter`. Each grant, revocation, suspension, etc. creates a new entry. `License.latest_record_id` points to the most recent `RecordLicense` entry.

Both tables are updated atomically in a single transaction whenever a license action occurs.

### `RecordEdit`
`RecordEdit` records in-game modifications to a `RecordCharacter` entry as a field/old_value/new_value triple. All
values are stored as strings via `ToString()` on the relevant type. This can include in-universe deletions (setting
`hidden
 = true`) which are just an edit to the `hidden` field. Admin OOC deletions (`deleted`) and edits do not create
 `RecordEdit` entries and are tracked only in the admin log. This is to ensure that OOC actions are never exposed though
  IC means. `last_edit_id` in the RecordCharacter table should point to the most recent RecordEdit associated
  with that record. Mutators to this table should also update the associated record's `last_edit_id`. Certain mutations
  may choose to not add to the edit history and thus should not get RecordEdit entries and thus does not touch
  `last_edit_id` either.

### Future Tables
The following record types are planned but not yet designed:
- `RecordEmployment`

### License Status Field
A `status` enum field (e.g. `provisional`, `suspended`) has been deferred from Licences pending further design. It can be added via migration if needed. Things reflecting information about the licence itself (e.g. provisional licences having less powers) should be present on the `Licence` table. Things reflecting the bureaucratic processes surrounding a licence (e.g. a temporary suspension) should be reflected on the appropriate `RecordLicense` reflecting the action taken.

---

## Tables

### `RecordCharacter` (Parent)

Holds data common to all record types. All child record tables PK inherits this table's `id` with a 1-1 relationship FK.

| Column                | Type      | Nullable | Notes                                                                                                |
| --------------------- | --------- | -------- | ---------------------------------------------------------------------------------------------------- |
| `id`                  | int       | NO       | Primary key                                                                                          |
| `target_character_id` | int (FK)  | YES      | → `profile.id`. The subject character. Nullable for records not tied to a specific character         |
| `round_id`            | int (FK)  | YES      | → `round.id`. Round in which the record was created. Nullable for records created outside a round    |
| `author_user_id`      | guid (FK) | YES      | → `preference.user_id`. Account that created the record.                                             |
| `author_character_id` | int (FK)  | YES      | → `profile.id`. Character who authored the record. Used to allow authors to modify their own records |
| `record_type`         | enum      | NO       | `personal_note`, `sle_note`, `medical_note`, `license`, `arrest`, `legal_case`, `employment_record`  |
| `created_at`          | timestamp | NO       | Server time at creation. Display with +500yr offset at application layer only                        |
| `last_edit_id`         | int (FK)  | YES      | → `record_edit.id` The most recent entry for that record in RecordEdit. Set in RecordEdit mutators.  |
| `hidden`              | bool      | NO       | IC soft delete. Hides from normal in-game display. Tracked via `RecordEdit`                          |
| `deleted`             | bool      | NO       | OOC/admin removal. Hides from all in-game display except admin tools.                                |
| `deleted_by_id`       | guid (FK) | YES      | → `preference.user_id`. Account that deleted the record                                              |
| `deleted_at`          | timestamp | YES      | Time of deletion                                                                                     |

---

### `RecordEdit`

Records in-game modifications to a `RecordCharacter` entry. Each change creates a new row. Admin OOC deletions are not recorded here, use admin logs for those.

| Column                | Type      | Nullable | Notes                                                               |
| --------------------- | --------- | -------- | ------------------------------------------------------------------- |
| `id`                  | int       | NO       | Primary key                                                         |
| `record_character_id` | int (FK)  | NO       | → `RecordCharacter.id`                                              |
| `field`               | string    | NO       | Name of the changed field                                           |
| `old_value`           | string    | YES      | Previous value via `ToString()`. Null if field was previously unset |
| `new_value`           | string    | YES      | New value via `ToString()`. Null if field was cleared               |
| `created_at`          | timestamp | NO       | Server time at edit                                                 |
| `author_user_id`      | guid (FK) | YES      | → `preference.user_id`. Nullable for system-generated edits         |
| `author_character_id` | int (FK)  | YES      | → `profile.id`. Character who made the edit                         |
| `deleted`             | bool      | NO       | OOC/admin removal only                                              |
| `deleted_by_id`       | guid (FK) | YES      | → `preference.user_id`                                              |
| `deleted_at`          | timestamp | YES      | Time of deletion                                                    |

---

### `RecordPersonalNote` (Child — 1-1)

A freeform personal note attached to a character record.

| Column                | Type         | Nullable | Notes                  |
| --------------------- | ------------ | -------- | ---------------------- |
| `record_character_id` | int (PK, FK) | NO       | → `RecordCharacter.id` |
| `title`               | string       | NO       |                        |
| `body`                | string       | NO       | Note content           |

---

### `RecordSLENote` (Child — 1-1)

Law enforcement notes on a character. Freeform note for SLE use.

| Column                     | Type         | Nullable | Notes                                                |
| -------------------------- | ------------ | -------- | ---------------------------------------------------- |
| `record_character_id`      | int (PK, FK) | NO       | → `RecordCharacter.id`                               |
| `title`                    | string       | NO       |                                                      |
| `body`                     | string       | NO       | Note content                                         |
| `authority_character_name` | string       | YES      | Snapshotted name of the authority at time of writing |
| `authority_rank`           | string       | YES      | Snapshotted rank of the authority at time of writing |

---

### `RecordMedicalNote` (Child — 1-1)

Medical notes on a character written by medical staff.

| Column                  | Type         | Nullable | Notes                                             |
| ----------------------- | ------------ | -------- | ------------------------------------------------- |
| `record_character_id`   | int (PK, FK) | NO       | → `RecordCharacter.id`                            |
| `title`                 | string       | NO       |                                                   |
| `body`                  | string       | NO       | Note content                                      |
| `doctor_character_name` | string       | YES      | Snapshotted name of the doctor at time of writing |
| `doctor_rank`           | string       | YES      | Snapshotted rank of the doctor at time of writing |

---

### `RecordArrest` (Child — 1-1)

Tracks arrests and their current status. Status is edited in place; transition history is tracked in `RecordEdits`. Planned, not yet implemented

| Column                     | Type         | Nullable | Notes                                                                                                   |
| -------------------------- | ------------ | -------- | ------------------------------------------------------------------------------------------------------- |
| `record_character_id`      | int (PK, FK) | NO       | → `RecordCharacter.id`                                                                                  |
| `reason`                   | string       | NO       | Reason for arrest                                                                                       |
| `arrested_at`              | timestamp    | NO       | Canonical in-universe arrest time. Separate from `created_at` as an arrest may be logged after the fact |
| `duration`                 | timespan     | YES      | Sentence duration. Release time = `arrested_at + duration`. Null = indefinite                           |
| `status`                   | enum         | NO       | `wanted`, `detained`, `released`, `paroled`                                                             |
| `authority_character_name` | string       | YES      | Snapshotted name of the arresting officer at time of writing                                            |
| `authority_rank`           | string       | YES      | Snapshotted rank of the arresting officer at time of writing                                            |

---

### `RecordLegalCase` (Child — 1-1)

Legal cases. Planned, not yet implemented.

| Column                | Type         | Nullable | Notes                                    |
| --------------------- | ------------ | -------- | ---------------------------------------- |
| `record_character_id` | int (PK, FK) | NO       | → `RecordCharacter.id`                   |
| `is_criminal_case`    | bool         | NO       | True = criminal case, False = civil case |
| `ruling`              | string       | NO       | The ruling of the case                   |
| `transcript`          | string       | YES      | The transcript of the case               |

---

### `RecordLegalCaseParty`

Tracks the parties involved in a legal case. One row per party per case. Allows stable profile-based lookups when a character can be resolved, with a freeform display name for all cases. Planned, not yet implemented.

| Column                | Type     | Nullable | Notes                                                                             |
| --------------------- | -------- | -------- | --------------------------------------------------------------------------------- |
| `id`                  | int      | NO       | Primary key                                                                       |
| `record_character_id` | int (FK) | NO       | → `RecordLegalCase.record_character_id`                                           |
| `role`                | enum     | NO       | One of: `judge`, `defendant`, `plaintiff`, `defendant_lawyer`, `plaintiff_lawyer` |
| `character_id`        | int (FK) | YES      | → `profile.id`. Nullable for unresolvable parties (e.g. "The People", NPCs)       |
| `display_name`        | string   | NO       | Snapshotted name as entered by the clerk at time of writing                       |

---

### `License`

Current state of a character's licenses. Row presence implies an active license. Used for fast gameplay checks without joining the record history. Updated atomically alongside `RecordLicense` in a transaction. Planned, not yet implemented

| Column                | Type     | Nullable | Notes                                                                   |
| --------------------- | -------- | -------- | ----------------------------------------------------------------------- |
| `id`                  | int      | NO       | Primary key                                                             |
| `target_character_id` | int (FK) | NO       | → `profile.id`. The license holder                                      |
| `license_type`        | string   | NO       | Free string (e.g. `"Weapons"`, `"Medical"`)                             |
| `latest_record_id`    | int (FK) | YES      | → `RecordLicense.record_character_id`. Points to the most recent action |

---

### `RecordLicense` (Child — 1-1)

Append-only history of license actions. Each grant, revocation, suspension, etc. creates a new `RecordCharacter` entry and a corresponding `RecordLicense` child. Never edited in place. Planned, not yet implemented

| Column                     | Type         | Nullable | Notes                                                      |
| -------------------------- | ------------ | -------- | ---------------------------------------------------------- |
| `record_character_id`      | int (PK, FK) | NO       | → `RecordCharacter.id`                                     |
| `license_id`               | int (FK)     | NO       | → `License.id`                                             |
| `action`                   | enum         | NO       | `granted`, `revoked`, `suspended`, `reinstated`, `expired` |
| `note`                     | string       | YES      | Optional descriptive note                                  |
| `authority_character_name` | string       | YES      | Snapshotted name of the authority at time of writing       |
| `authority_rank`           | string       | YES      | Snapshotted rank of the authority at time of writing       |
| `expires_at`               | timestamp    | YES      | Expiry time. Null = does not expire                        |

---

## ERD
```mermaid
erDiagram
    preference {
        int id PK
        guid user_id "unique"
    }

    profile {
        int id PK
        int preference_id FK
        string char_name
    }

    RecordCharacter {
        int id PK
        int target_character_id FK
        int round_id FK
        guid author_user_id FK
        int author_character_id FK
        enum record_type "personal_note | sle_note | medical_note | license | arrest | legal_case | employment_record"
        timestamp created_at "server time; +500yr display only"
        int last_edit_id FK
        bool hidden "IC soft delete; tracked via RecordEdit"
        bool deleted "OOC/admin removal only"
        guid deleted_by_id FK
        timestamp deleted_at
    }

    RecordEdit {
        int id PK
        int record_character_id FK
        string field "NOT NULL; name of the changed field"
        string old_value "ToString() of previous value"
        string new_value "ToString() of new value"
        timestamp created_at
        guid author_user_id FK
        int author_character_id FK
        bool deleted "OOC/admin removal only"
        guid deleted_by_id FK
        timestamp deleted_at
    }

    RecordPersonalNote {
        int record_character_id PK "also FK -> RecordCharacter.id"
        string title "NOT NULL"
        string body "NOT NULL"
    }

    RecordSLENote {
        int record_character_id PK "also FK -> RecordCharacter.id"
        string title "NOT NULL"
        string body "NOT NULL"
        string authority_character_name
        string authority_rank
    }

    RecordMedicalNote {
        int record_character_id PK "also FK -> RecordCharacter.id"
        string title "NOT NULL"
        string body "NOT NULL"
        string doctor_character_name
        string doctor_rank
    }

    RecordArrest {
        int record_character_id PK "also FK -> RecordCharacter.id"
        string reason "NOT NULL"
        timestamp arrested_at
        timespan duration
        enum status "wanted | detained | released | paroled"
        string authority_character_name
        string authority_rank
    }

    RecordLegalCase {
        int record_character_id PK "also FK -> RecordCharacter.id"
        bool is_criminal_case "NOT NULL"
        string ruling "NOT NULL"
        string transcript
    }

    RecordLegalCaseParty {
	    int id PK
		int record_character_id
		FK "-> RecordLegalCase"
		enum role "judge | defendant | plaintiff | defendant_lawyer | plaintiff_lawyer"
		int character_id FK "nullable; -> profile.id"
		string display_name "NOT NULL"
	}

    License {
        int id PK
        int target_character_id FK
        string license_type "NOT NULL"
        int latest_record_id FK
    }

    RecordLicense {
        int record_character_id PK "also FK -> RecordCharacter.id"
        int license_id FK
        enum action "granted | revoked | suspended | reinstated | expired"
        string note
        string authority_character_name
        string authority_rank
        timestamp expires_at
    }

    preference ||--o{ profile : "owns"
    preference ||--o{ RecordCharacter : "authors"
    preference ||--o{ RecordCharacter : "deleted_by"
    profile ||--o{ RecordCharacter : "target character"
    profile ||--o{ License : "holds"
    RecordCharacter ||--o| RecordPersonalNote : "child"
    RecordCharacter ||--o| RecordSLENote : "child"
    RecordCharacter ||--o| RecordMedicalNote : "child"
    RecordCharacter ||--o| RecordArrest : "child"
    RecordCharacter ||--o| RecordLegalCase : "child"
    RecordCharacter ||--o| RecordLicense : "child"
    RecordCharacter ||--o{ RecordEdit : "modifications"
    License ||--o{ RecordLicense : "history"
    License ||--o| RecordLicense : "latest_record"
    RecordLegalCase ||--o{ RecordLegalCaseParty : "parties"
    profile ||--o{ RecordLegalCaseParty : "party reference"
```
