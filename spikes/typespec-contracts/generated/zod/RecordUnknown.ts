import { z } from "zod"

export const RecordUnknownSchema = z.object({})
export type RecordUnknownSchema = z.infer<typeof RecordUnknownSchema>
