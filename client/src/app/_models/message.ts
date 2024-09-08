export interface Message {
  id: number
  senderId: number
  sendUsername: string
  senderPhotoUrl: string
  recipientId: number
  recipientUsername: string
  recipientPhotoUrl: string
  content: string
  dateRead?: Date
  messageSent: Date
}
