import { type MetadataRelationship } from './report-metadata-api'

export type RelationshipDto = MetadataRelationship

export type RelationshipRequest = {
  fromTableId: string
  fromColumn: string
  toTableId: string
  toColumn: string
  cardinality: '1:1' | '1:N' | 'N:1' | 'N:N'
  joinType: 'INNER' | 'LEFT'
  crossFilterDirection: 'single' | 'both'
  isActive: boolean
  isPrimary: boolean
}

export type AutodetectRelationshipsResponse = {
  relationships: RelationshipDto[]
  summary: {
    detected: number
    databaseForeignKeys: number
    inferredByName: number
    skippedExisting: number
    warnings: string[]
  }
}

const API_BASE = process.env.NEXT_PUBLIC_REPORT_API_URL ?? 'http://localhost:5000'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })

  if (!res.ok) {
    throw new Error(await res.text())
  }

  if (res.status === 204) return undefined as T
  return res.json()
}

export function getRelationships(datasetId: string) {
  return request<RelationshipDto[]>(`/api/datasets/${encodeURIComponent(datasetId)}/relationships`)
}

export function createRelationship(datasetId: string, body: RelationshipRequest) {
  return request<RelationshipDto>(`/api/datasets/${encodeURIComponent(datasetId)}/relationships`, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

export function updateRelationship(datasetId: string, relationshipId: string, body: RelationshipRequest) {
  return request<RelationshipDto>(`/api/datasets/${encodeURIComponent(datasetId)}/relationships/${encodeURIComponent(relationshipId)}`, {
    method: 'PUT',
    body: JSON.stringify({ ...body, relationshipId }),
  })
}

export function deleteRelationship(datasetId: string, relationshipId: string) {
  return request<void>(`/api/datasets/${encodeURIComponent(datasetId)}/relationships/${encodeURIComponent(relationshipId)}`, {
    method: 'DELETE',
  })
}

export function autodetectRelationships(datasetId: string) {
  return request<AutodetectRelationshipsResponse>(`/api/datasets/${encodeURIComponent(datasetId)}/relationships/autodetect`, {
    method: 'POST',
    body: JSON.stringify({ datasetId, mode: 'safe', includeExisting: false }),
  })
}

export function activateRelationship(datasetId: string, relationshipId: string) {
  return request<RelationshipDto[]>(`/api/datasets/${encodeURIComponent(datasetId)}/relationships/${encodeURIComponent(relationshipId)}/activate`, {
    method: 'POST',
  })
}
