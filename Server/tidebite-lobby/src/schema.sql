PRAGMA foreign_keys=ON;
CREATE TABLE IF NOT EXISTS users (
 id TEXT PRIMARY KEY, login TEXT NOT NULL UNIQUE COLLATE NOCASE,
 password_hash TEXT NOT NULL, display_name TEXT NOT NULL,
 avatar INTEGER NOT NULL DEFAULT 0, created_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS sessions (
 token_hash BLOB PRIMARY KEY, user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
 expires_at INTEGER NOT NULL, created_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS sessions_user ON sessions(user_id);
CREATE TABLE IF NOT EXISTS admissions (
 id TEXT PRIMARY KEY, user_id TEXT REFERENCES users(id) ON DELETE SET NULL,
 room TEXT NOT NULL, expires_at INTEGER NOT NULL, accepted_at INTEGER, last_seen INTEGER
);
CREATE INDEX IF NOT EXISTS admissions_user ON admissions(user_id);
CREATE TABLE IF NOT EXISTS rounds (
 id TEXT NOT NULL, room TEXT NOT NULL, payload_hash BLOB NOT NULL,
 duration INTEGER NOT NULL, played_at INTEGER NOT NULL,
 PRIMARY KEY(room,id)
);
CREATE TABLE IF NOT EXISTS results (
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
 room TEXT NOT NULL, round_id TEXT NOT NULL, admission_id TEXT NOT NULL,
 score INTEGER NOT NULL, kills INTEGER NOT NULL, assists INTEGER NOT NULL,
 level INTEGER NOT NULL, rank INTEGER NOT NULL, completed INTEGER NOT NULL,
 played_at INTEGER NOT NULL, UNIQUE(user_id,room,round_id),
 FOREIGN KEY(room,round_id) REFERENCES rounds(room,id)
);
CREATE INDEX IF NOT EXISTS results_user ON results(user_id,id DESC);
CREATE INDEX IF NOT EXISTS results_period ON results(played_at,user_id);
PRAGMA user_version=1;
