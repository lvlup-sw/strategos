import { z } from "zod"

export const TddPhaseSchema = z.enum(["red","green","refactor"]).describe("TDD cycle phase for task progress tracking.")
export type TddPhaseSchema = z.infer<typeof TddPhaseSchema>
