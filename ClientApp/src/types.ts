export type Role = "Admin" | "Teacher" | "Student";
export interface Identity {
  id: string;
  fullName: string;
  email: string;
  role: Role;
  permissionCodes: string[];
}
export interface Department {
  id: string;
  code: string;
  name: string;
  description: string;
}
export interface Subject {
  id: string;
  departmentId: string;
  code: string;
  name: string;
  description: string;
}
export interface ClassRoom {
  id: string;
  name: string;
  description: string;
  subjectId: string;
  studentIds: string[];
  enrollmentCapacity: number;
}
export interface ClassSummary {
  id: string;
  name: string;
  description: string;
  subjectName: string;
  teacherName: string;
  enrolledCount: number;
  enrollmentCapacity: number;
}
export interface Lesson {
  id: string;
  title: string;
  content: string;
  isPublished: boolean;
}
export interface Assignment {
  id: string;
  title: string;
  description: string;
  dueDate: string;
  isPublished: boolean;
}
