import { z } from "zod"

export const FindingSeveritySchema = z.enum(["critical","major","minor","suggestion"]).describe("Review finding severity levels.")
export type FindingSeveritySchema = z.infer<typeof FindingSeveritySchema>
