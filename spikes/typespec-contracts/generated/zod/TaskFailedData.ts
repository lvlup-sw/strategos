import { z } from "zod"

export const TaskFailedDataSchema = z.object({ "taskId": z.string(), "error": z.string().max(500), "diagnostics": z.any().optional() }).describe("Data payload for `task.failed` events.")
export type TaskFailedDataSchema = z.infer<typeof TaskFailedDataSchema>
