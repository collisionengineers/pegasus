export interface StoredObject {
  body: Buffer;
  contentType: string;
  filename: string;
}

export interface PutObjectInput extends StoredObject {
  key: string;
}

export interface ObjectStore {
  put(input: PutObjectInput): Promise<void>;
  get(key: string): Promise<StoredObject>;
  delete(key: string): Promise<void>;
  exists(key: string): Promise<boolean>;
  deleteExpired(prefix: string, before: Date): Promise<number>;
}
