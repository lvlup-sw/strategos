import { z } from "zod"

export const TeamTaskCompletedDataSchema = z.object({ "taskId": z.string(), "teammateName": z.string(), "durationMs": z.number(), "filesChanged": z.array(z.string()), "testsPassed": z.boolean(), "qualityGateResults": z.any() }).describe("Data payload for `team.task.completed` events.")
export type TeamTaskCompletedDataSchema = z.infer<typeof TeamTaskCompletedDataSchema>
