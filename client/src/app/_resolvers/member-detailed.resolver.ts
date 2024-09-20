import { ResolveFn } from '@angular/router';
import { Member } from '../_models/member';
import { inject } from '@angular/core';
import { MembersService } from '../_services/members.service';

export const memberDetailedResolver: ResolveFn<Member | null> = (route, state) => {
  const memberService = inject(MembersService);

  const userName = route.paramMap.get('username');

  if (!userName) return null;

  return memberService.getMember(userName);
};
