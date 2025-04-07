CREATE SEQUENCE IF NOT EXISTS Documents_Seq;

CREATE TABLE IF NOT EXISTS Documents (
    id INTEGER DEFAULT nextval('Documents_Seq') PRIMARY KEY,
    title TEXT,
    metadata JSON,
    content TEXT
);

CREATE SEQUENCE IF NOT EXISTS embeddings_id_seq;

CREATE TABLE IF NOT EXISTS embeddings (
    id INTEGER DEFAULT nextval('embeddings_id_seq') PRIMARY KEY,
    text TEXT NOT NULL,
    embedding FLOAT[],
    document_id INTEGER,
    FOREIGN KEY(document_id) REFERENCES Documents(id)
);

CREATE SEQUENCE IF NOT EXISTS Chunks_Seq;

CREATE TABLE IF NOT EXISTS Chunks (
    id INTEGER DEFAULT nextval('Documents_Seq') PRIMARY KEY,
    document_id INTEGER,
    chunk_text TEXT,
    FOREIGN KEY(document_id) REFERENCES Documents(id)
);
