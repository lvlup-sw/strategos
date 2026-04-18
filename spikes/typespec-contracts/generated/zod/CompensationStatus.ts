import { z } from "zod"

export const CompensationStatusSchema = z.enum(["executed","skipped","failed","dry-run"]).describe("Compensation action status.")
export type CompensationStatusSchema = z.infer<typeof CompensationStatusSchema>
