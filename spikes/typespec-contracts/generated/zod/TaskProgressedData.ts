import { z } from "zod"

export const TaskProgressedDataSchema = z.object({ "taskId": z.string(), "tddPhase": z.any(), "detail": z.string().max(500).optional() }).describe("Data payload for `task.progressed` events.")
export type TaskProgressedDataSchema = z.infer<typeof TaskProgressedDataSchema>
