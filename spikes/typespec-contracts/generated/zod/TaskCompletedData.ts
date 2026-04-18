import { z } from "zod"

export const TaskCompletedDataSchema = z.object({ "taskId": z.string(), "acceptanceTestRef": z.string().optional(), "artifacts": z.array(z.string()).optional(), "duration": z.number().optional(), "evidence": z.any().optional(), "verified": z.boolean().optional(), "implements": z.array(z.string()).optional(), "tests": z.array(z.any()).optional(), "files": z.array(z.string()).optional() }).describe("Data payload for `task.completed` events.")
export type TaskCompletedDataSchema = z.infer<typeof TaskCompletedDataSchema>
